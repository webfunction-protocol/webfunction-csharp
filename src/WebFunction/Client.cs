using System.Dynamic;
using WebFunction.Exceptions;

namespace WebFunction;

/// <summary>
/// A wrapper around a <see cref="Package"/> that provides dynamic-dispatch invocation of its
/// endpoints, the same primary-API shape as the Ruby/JS/PHP reference clients.
/// </summary>
/// <example>
/// <code>
/// dynamic client = await Client.FromPackageEndpointAsync("https://api.example.com/package");
/// var result = await client.ListItems(new Dictionary&lt;string, object?&gt; { ["a"] = "b" });
/// </code>
/// </example>
public sealed class Client : DynamicObject
{
    private readonly Dictionary<string, string> _endpointsByMemberName;

    private Client(Package package, string? bearerAuth, string? version, Pipeline? pipeline)
    {
        Package = package;
        BearerAuth = bearerAuth;
        Version = version;
        Pipeline = pipeline;

        // Map every possible PascalCase-ish dynamic member name back to the real (possibly
        // hyphenated) wire endpoint name, so `client.ListItems(...)` finds "list-items".
        _endpointsByMemberName = package.Endpoints.ToDictionary(e => Naming.Dashify(e.Name), e => e.Name);

        foreach (var endpoint in package.Endpoints)
        {
            endpoint.Client = this;
        }
    }

    /// <summary>The package this client wraps.</summary>
    public Package Package { get; }

    /// <summary>The bearer authentication token used for subsequent calls.</summary>
    public string? BearerAuth { get; set; }

    /// <summary>The API version sent with subsequent calls.</summary>
    public string? Version { get; set; }

    /// <summary>The pipeline subsequent calls are queued onto, or <c>null</c> for immediate (non-pipelined) calls.</summary>
    public Pipeline? Pipeline { get; set; }

    /// <summary>Creates a client by POSTing to <paramref name="url"/> as a Web Function package endpoint.</summary>
    public static async Task<Client> FromPackageEndpointAsync(string url, string? bearerAuth = null, string? version = null, bool pipelined = false)
    {
        var response = await RequestExecutor.ExecuteAsync(url, bearerAuth, version, new Dictionary<string, object?>()).ConfigureAwait(false);
        var package = ParsePackage(response);
        return FromPackage(package, bearerAuth, version, pipelined);
    }

    /// <summary>Creates a client by fetching <paramref name="url"/> as plain JSON via GET (version sent as an <c>api_version</c> query param).</summary>
    public static async Task<Client> FromUrlAsync(string url, string? bearerAuth = null, string? version = null, bool pipelined = false)
    {
        var response = await RequestExecutor.GetAsync(url, version).ConfigureAwait(false);
        var package = ParsePackage(response);
        return FromPackage(package, bearerAuth, version, pipelined);
    }

    /// <summary>Creates a client directly from an in-memory <see cref="Package"/>, with no request.</summary>
    public static Client FromPackage(Package package, string? bearerAuth = null, string? version = null, bool pipelined = false)
    {
        var pipeline = pipelined ? package.BuildPipeline() : null;
        return new Client(package, bearerAuth, version, pipeline);
    }

    private static Package ParsePackage(object? decoded)
    {
        if (decoded is not Dictionary<string, object?> dict)
        {
            throw new JsonParseException("Expected a package object at the top level");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(Json.Serialize(dict));
        return Package.FromJson(doc.RootElement);
    }

    /// <summary>Calls an endpoint by name (hyphenated wire name or a PascalCase/underscored equivalent) with the given arguments.</summary>
    /// <returns>The decoded response — a <see cref="Page"/> if the endpoint is flagged <c>paginated</c>, otherwise the raw decoded value.</returns>
    public async Task<object?> CallAsync(string endpointName, Dictionary<string, object?>? args = null)
    {
        var wireName = _endpointsByMemberName.TryGetValue(Naming.Dashify(endpointName), out var mapped)
            ? mapped
            : Naming.Dashify(endpointName);

        var endpoint = Package.GetEndpoint(wireName);
        var url = new Uri(new Uri(Package.BaseUrl), wireName).ToString();
        var headers = RequestExecutor.BuildHeaders(BearerAuth, Version);

        if (Pipeline is not null)
        {
            return Pipeline.AddStep(url, headers, args ?? new Dictionary<string, object?>());
        }

        var result = await RequestExecutor.ExecuteAsync(url, BearerAuth, Version, args ?? new Dictionary<string, object?>()).ConfigureAwait(false);

        return endpoint is { Paginated: true } ? Page.From(result, url, BearerAuth, Version) : result;
    }

    /// <inheritdoc />
    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        if (!_endpointsByMemberName.ContainsKey(Naming.Dashify(binder.Name)))
        {
            result = null;
            return false;
        }

        var callArgs = args is { Length: > 0 } ? args[0] as Dictionary<string, object?> : null;
        result = CallAsync(binder.Name, callArgs);
        return true;
    }

    /// <inheritdoc />
    public override IEnumerable<string> GetDynamicMemberNames() => _endpointsByMemberName.Keys;
}
