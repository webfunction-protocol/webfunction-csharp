namespace WebFunction.Type;

/// <summary>A union of two or more possible types (e.g. a nullable field is <c>string | null</c>).</summary>
public sealed class UnionType : WfnType
{
    public UnionType(IReadOnlyList<WfnType> types) => Types = types;

    public IReadOnlyList<WfnType> Types { get; }

    public override bool Valid(object? value) => Types.Any(t => t.Valid(value));

    public override string ToString() => string.Join(" | ", Types);
}
