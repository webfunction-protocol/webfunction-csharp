namespace WebFunction.Type;

/// <summary>An <c>array</c> of a given element type.</summary>
public sealed class ArrayOfType : WfnType
{
    public ArrayOfType(WfnType of) => Of = of;

    /// <summary>The element type.</summary>
    public WfnType Of { get; }

    public override string ToString() => $"array.of({Of})";
}
