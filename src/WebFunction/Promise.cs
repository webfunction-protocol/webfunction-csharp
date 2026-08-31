using WebFunction.Exceptions;

namespace WebFunction;

/// <summary>
/// A placeholder for a value that will be resolved once its owning <see cref="Pipeline"/>
/// executes. Before resolution it serializes as its JSONPath reference string (see
/// <see cref="Json.Serialize"/>), so an unresolved promise can be passed straight back in as
/// another call's argument — the server resolves the reference itself.
/// </summary>
public sealed class Promise
{
    private readonly Pipeline _pipeline;
    private object? _value;

    internal Promise(Pipeline pipeline, Path path)
    {
        _pipeline = pipeline;
        Path = path;
    }

    /// <summary>The JSONPath expression this promise resolves to, before resolution.</summary>
    public Path Path { get; }

    /// <summary>Whether this promise has been resolved (its owning pipeline has executed).</summary>
    public bool IsResolved { get; private set; }

    /// <summary>
    /// The resolved value.
    /// </summary>
    /// <exception cref="UnresolvedPromiseException">The pipeline has not executed yet.</exception>
    public object? Value => IsResolved ? _value : throw new UnresolvedPromiseException();

    internal void SetValue(object? value)
    {
        _value = value;
        IsResolved = true;
    }

    /// <summary>
    /// Returns a promise (or, once resolved, a value) scoped to the given object key —
    /// e.g. <c>promise.Field("id")</c> for a step whose result will contain an <c>id</c> field.
    /// </summary>
    public object? Field(string key) => IsResolved ? Navigate(_value, key) : new Promise(_pipeline, Path.Field(key));

    /// <summary>Returns a promise (or, once resolved, a value) scoped to the given array index.</summary>
    public object? Index(int index) => IsResolved ? Navigate(_value, index) : new Promise(_pipeline, Path.Index(index));

    /// <summary>
    /// Resolves this promise, executing its owning pipeline first if it hasn't already executed.
    /// </summary>
    public async Task<object?> ResolveAsync()
    {
        if (IsResolved)
        {
            return _value;
        }

        await _pipeline.ExecuteAsync().ConfigureAwait(false);
        return Value;
    }

    public override string ToString() => IsResolved ? _value?.ToString() ?? "null" : Path.ToString();

    private static object? Navigate(object? value, string key) =>
        value is IDictionary<string, object?> dict && dict.TryGetValue(key, out var v) ? v : null;

    private static object? Navigate(object? value, int index) =>
        value is IList<object?> list && index >= 0 && index < list.Count ? list[index] : null;
}
