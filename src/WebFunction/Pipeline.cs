namespace WebFunction;

/// <summary>
/// A sequence of steps executed together in a single request against a package's
/// <c>pipeline_url</c>. <see cref="AddStep"/> returns a <see cref="Promise"/> for that step's
/// eventual result, which can itself be passed as another step's argument (resolved server-side
/// via its JSONPath reference).
/// </summary>
public sealed class Pipeline
{
    private readonly string _url;
    private readonly List<Dictionary<string, object?>> _steps = new();
    private readonly List<Promise> _promises = new();

    public Pipeline(string url) => _url = url;

    /// <summary>Adds a step and returns a <see cref="Promise"/> scoped to its eventual result.</summary>
    public Promise AddStep(string url, Dictionary<string, string> headers, object? body)
    {
        var n = _promises.Count;
        var promise = new Promise(this, new Path($"$[{n}]"));

        _steps.Add(new Dictionary<string, object?> { ["url"] = url, ["headers"] = headers, ["body"] = body });
        _promises.Add(promise);

        return promise;
    }

    /// <summary>
    /// Executes every queued step in one request. <paramref name="returns"/> controls what the
    /// server sends back: <c>"$"</c> (default) returns every step's result and resolves every
    /// promise from this batch; <c>"$[-1:]"</c> returns only the final step's result (resolving
    /// only its promise); any other JSONPath expression is passed through verbatim for the server
    /// to resolve directly, resolving nothing on this side. Resets the pipeline afterwards.
    /// </summary>
    public async Task<object?> ExecuteAsync(string returns = "$")
    {
        var args = new Dictionary<string, object?> { ["steps"] = _steps, ["returns"] = returns };
        var response = await RequestExecutor.ExecuteAsync(_url, null, null, args).ConfigureAwait(false);

        switch (returns)
        {
            case "$":
                if (response is List<object?> all)
                {
                    for (var i = 0; i < all.Count && i < _promises.Count; i++)
                    {
                        _promises[i].SetValue(all[i]);
                    }
                }

                break;
            case "$[-1:]":
                if (_promises.Count > 0)
                {
                    _promises[^1].SetValue(response);
                }

                break;
        }

        Reset();
        return response;
    }

    /// <summary>Discards all queued steps and promises without executing them.</summary>
    public void Reset()
    {
        _steps.Clear();
        _promises.Clear();
    }
}
