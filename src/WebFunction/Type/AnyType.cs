namespace WebFunction.Type;

/// <summary>An unconstrained type — matches any value.</summary>
public sealed class AnyType : WfnType
{
    public override string ToString() => "any";
}
