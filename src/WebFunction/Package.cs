using System.Text.Json;

namespace WebFunction;

/// <summary>
/// A parsed Web Function package: the base URL, its endpoints, and optional metadata
/// (name/version/docs, common errors, named object definitions).
/// </summary>
public sealed class Package
{
    private readonly Dictionary<string, Endpoint> _endpoints;
    private readonly Dictionary<string, DocumentedError> _errors;
    private readonly Dictionary<string, ObjectSchema> _objects;

    public Package(
        string baseUrl,
        string? pipelineUrl,
        string? name,
        string? version,
        string docs,
        IReadOnlyList<string> flags,
        IReadOnlyList<string> versions,
        IReadOnlyList<Endpoint> endpoints,
        IReadOnlyList<DocumentedError> errors,
        IReadOnlyList<ObjectSchema> objects)
    {
        BaseUrl = baseUrl;
        PipelineUrl = pipelineUrl;
        Name = name;
        Version = version;
        Docs = docs;
        Flags = flags;
        Versions = versions;
        _endpoints = endpoints.ToDictionary(e => e.Name);
        _errors = errors.ToDictionary(e => e.Code);
        _objects = objects.ToDictionary(o => o.Name);
    }

    public string BaseUrl { get; }
    public string? PipelineUrl { get; }
    public string? Name { get; }
    public string? Version { get; }
    public string Docs { get; }
    public IReadOnlyList<string> Flags { get; }
    public IReadOnlyList<string> Versions { get; }

    public bool Versioned => Flags.HasFlag("versioned");

    /// <summary>A <see cref="Pipeline"/> for this package, or <c>null</c> if it declares no <see cref="PipelineUrl"/>.</summary>
    public Pipeline? BuildPipeline() => PipelineUrl is null ? null : new Pipeline(PipelineUrl);

    public IReadOnlyList<Endpoint> Endpoints => _endpoints.Values.ToList();

    /// <summary>Looks up an endpoint by name. Underscores are converted to hyphens, so a PascalCase-derived lookup matches wire names.</summary>
    public Endpoint? GetEndpoint(string name) => _endpoints.GetValueOrDefault(Naming.Dashify(name));

    public IReadOnlyList<DocumentedError> Errors => _errors.Values.ToList();
    public DocumentedError? GetError(string code) => _errors.GetValueOrDefault(code);

    public IReadOnlyList<ObjectSchema> Objects => _objects.Values.ToList();

    /// <summary>
    /// Looks up a named object, resolved for the given context — <c>null</c> if none is found, or
    /// if it defines no members for that context (treated as absent in that context).
    /// </summary>
    public ObjectSchema? ObjectInContext(string name, ObjectContext context)
    {
        var obj = _objects.GetValueOrDefault(name);
        if (obj is null || obj.Properties(context).Count == 0)
        {
            return null;
        }

        return obj;
    }

    public static Package FromJson(JsonElement element)
    {
        var package = new Package(
            element.GetProperty("base_url").GetString()!,
            element.TryGetProperty("pipeline_url", out var pu) && pu.ValueKind == JsonValueKind.String ? pu.GetString() : null,
            element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null,
            element.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null,
            element.TryGetProperty("docs", out var d) ? d.GetString() ?? "" : "",
            element.TryGetProperty("flags", out var f) ? JsonUtil.StringArray(f) : Array.Empty<string>(),
            element.TryGetProperty("versions", out var vs) ? JsonUtil.StringArray(vs) : Array.Empty<string>(),
            element.TryGetProperty("endpoints", out var e) ? Endpoint.FromArray(e) : new(),
            element.TryGetProperty("errors", out var errs) ? DocumentedError.FromArray(errs) : new(),
            element.TryGetProperty("objects", out var objs) ? ObjectSchema.FromArray(objs) : new());

        return package;
    }
}
