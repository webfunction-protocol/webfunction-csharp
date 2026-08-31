using System.Text.Json;
using WebFunction.Type;

namespace WebFunction;

/// <summary>An operation described in a package: its name, documentation, arguments, attributes, and possible errors.</summary>
public sealed class Endpoint
{
    private readonly Dictionary<string, Argument> _arguments;
    private readonly Dictionary<string, AttributeDef> _attributes;
    private readonly Dictionary<string, DocumentedError> _errors;

    public Endpoint(
        string name,
        WfnType returns,
        IReadOnlyList<string> flags,
        string? group,
        string docs,
        IReadOnlyList<Argument> arguments,
        IReadOnlyList<AttributeDef> attributes,
        IReadOnlyList<DocumentedError> errors)
    {
        Name = name;
        Returns = returns;
        Flags = flags;
        Group = group;
        Docs = docs;
        _arguments = arguments.ToDictionary(a => a.Name);
        _attributes = attributes.ToDictionary(a => a.Name);
        _errors = errors.ToDictionary(e => e.Code);
    }

    /// <summary>The client this endpoint was loaded from. Set by <see cref="Package"/>/<see cref="Client"/> construction, required by <see cref="CallAsync"/>.</summary>
    public Client? Client { get; internal set; }

    public string Name { get; }
    public WfnType Returns { get; }
    public IReadOnlyList<string> Flags { get; }
    public string? Group { get; }
    public string Docs { get; }

    public IReadOnlyList<Argument> Arguments => _arguments.Values.ToList();
    public Argument? GetArgument(string name) => _arguments.GetValueOrDefault(name);

    public IReadOnlyList<AttributeDef> Attributes => _attributes.Values.ToList();
    public AttributeDef? GetAttribute(string name) => _attributes.GetValueOrDefault(name);

    public IReadOnlyList<DocumentedError> Errors => _errors.Values.ToList();
    public DocumentedError? GetError(string code) => _errors.GetValueOrDefault(code);

    public bool BearerAuth => Flags.HasFlag("bearer_auth");
    public bool CaptureBearer => Flags.HasFlag("capture_bearer");
    public bool Paginated => Flags.HasFlag("paginated");
    public bool Private => Flags.HasFlag("private");

    /// <summary>Invokes this endpoint through its assigned <see cref="Client"/>.</summary>
    /// <exception cref="InvalidOperationException">No client has been assigned.</exception>
    public Task<object?> CallAsync(Dictionary<string, object?>? args = null)
    {
        if (Client is null)
        {
            throw new InvalidOperationException("Client must be set to invoke an endpoint");
        }

        return Client.CallAsync(Name, args);
    }

    public static Endpoint? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("name", out var nameEl) ||
            !element.TryGetProperty("returns", out var returnsEl))
        {
            return null;
        }

        return new Endpoint(
            nameEl.GetString()!,
            WfnType.Parse(returnsEl),
            element.TryGetProperty("flags", out var f) ? JsonUtil.StringArray(f) : Array.Empty<string>(),
            element.TryGetProperty("group", out var g) ? g.GetString() : null,
            element.TryGetProperty("docs", out var d) ? d.GetString() ?? "" : "",
            element.TryGetProperty("arguments", out var a) ? Argument.FromArray(a) : new(),
            element.TryGetProperty("attributes", out var attrs) ? AttributeDef.FromArray(attrs) : new(),
            element.TryGetProperty("errors", out var e) ? DocumentedError.FromArray(e) : new());
    }

    public static List<Endpoint> FromArray(JsonElement? array) => JsonUtil.MapArray(array, FromJson);
}
