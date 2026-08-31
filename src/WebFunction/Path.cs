namespace WebFunction;

/// <summary>
/// An immutable JSONPath expression that can be used to resolve a value from a pipelined
/// response. Ported from the reference Ruby gem's <c>Promise::Path</c>.
/// </summary>
public sealed class Path
{
    private readonly string _path;

    public Path(string path) => _path = path;

    /// <summary>Returns a new, deeper path addressing the given object key.</summary>
    public Path Field(string key) => new($"{_path}.{key}");

    /// <summary>Returns a new, deeper path addressing the given array index.</summary>
    public Path Index(int index) => new($"{_path}[{index}]");

    public override string ToString() => _path;
}
