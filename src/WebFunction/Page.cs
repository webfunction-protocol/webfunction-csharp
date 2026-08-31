using System.Collections;

namespace WebFunction;

/// <summary>
/// A page of results from a paginated endpoint. <c>next</c>/<c>previous</c> are opaque request
/// bodies — call <see cref="NextPageAsync"/>/<see cref="PreviousPageAsync"/> to fetch the adjacent
/// page; never construct or inspect them yourself.
/// </summary>
public sealed class Page : IEnumerable<object?>
{
    private readonly object? _nextBody;
    private readonly object? _previousBody;
    private readonly string _url;
    private readonly string? _bearerAuth;
    private readonly string? _version;

    private Page(List<object?> items, object? nextBody, object? previousBody, string url, string? bearerAuth, string? version)
    {
        Items = items;
        _nextBody = nextBody;
        _previousBody = previousBody;
        _url = url;
        _bearerAuth = bearerAuth;
        _version = version;
    }

    /// <summary>The items on this page.</summary>
    public IReadOnlyList<object?> Items { get; }

    /// <summary>Whether a next page is available.</summary>
    public bool HasNext => _nextBody is not null;

    /// <summary>Whether a previous page is available.</summary>
    public bool HasPrevious => _previousBody is not null;

    /// <summary>Fetches the next page, or <c>null</c> if there is none.</summary>
    public Task<Page?> NextPageAsync() => FetchAsync(_nextBody);

    /// <summary>Fetches the previous page, or <c>null</c> if there is none.</summary>
    public Task<Page?> PreviousPageAsync() => FetchAsync(_previousBody);

    public IEnumerator<object?> GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private async Task<Page?> FetchAsync(object? body)
    {
        if (body is null)
        {
            return null;
        }

        var result = await RequestExecutor.ExecuteAsync(_url, _bearerAuth, _version, body).ConfigureAwait(false);
        return From(result, _url, _bearerAuth, _version);
    }

    /// <summary>
    /// Builds a <see cref="Page"/> from a decoded response that is expected to match the
    /// pagination contract (<c>page</c>/<c>next</c>/<c>previous</c> keys). Called only when the
    /// invoking endpoint declares the <c>paginated</c> flag.
    /// </summary>
    /// <exception cref="InvalidOperationException">The response does not match the pagination contract.</exception>
    public static Page From(object? decoded, string url, string? bearerAuth, string? version)
    {
        if (decoded is not IDictionary<string, object?> dict ||
            !dict.TryGetValue("page", out var pageRaw) || pageRaw is not List<object?> page ||
            !dict.TryGetValue("next", out var next) ||
            !dict.TryGetValue("previous", out var previous))
        {
            throw new InvalidOperationException(
                "Endpoint is flagged as paginated, but its response did not match the pagination contract (expected page/next/previous keys)");
        }

        return new Page(page, next, previous, url, bearerAuth, version);
    }
}
