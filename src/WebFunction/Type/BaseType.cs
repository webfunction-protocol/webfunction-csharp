namespace WebFunction.Type;

/// <summary>A base JSON type (<c>string</c>, <c>number</c>, <c>object</c>, <c>boolean</c>, <c>null</c>), optionally refined.</summary>
public sealed class BaseType : WfnType
{
    public BaseType(string baseTypeName, string? refinement)
    {
        BaseTypeName = baseTypeName;
        Refinement = refinement;
    }

    /// <summary>One of <c>string</c>, <c>number</c>, <c>object</c>, <c>boolean</c>, <c>null</c>.</summary>
    public string BaseTypeName { get; }

    /// <summary>
    /// The refinement, if any (e.g. <c>email</c> for <c>string.email</c>, the referenced object
    /// name for <c>object.&lt;name&gt;</c>). <c>null</c> for an unrefined base type.
    /// </summary>
    public string? Refinement { get; }

    public override string ToString() => Refinement is null ? BaseTypeName : $"{BaseTypeName}.{Refinement}";
}
