using System.Text.Json;
using WebFunction.Type;

namespace WebFunction;

/// <summary>An output field an endpoint's object response may contain, as declared under <c>attributes</c>.</summary>
public sealed class AttributeDef
{
    public AttributeDef(string name, WfnType type, IReadOnlyList<object?> values, IReadOnlyList<string> flags, string docs)
    {
        Name = name;
        AttrType = type;
        Values = values;
        Flags = flags;
        Docs = docs;
    }

    public string Name { get; }
    public WfnType AttrType { get; }
    public IReadOnlyList<object?> Values { get; }
    public IReadOnlyList<string> Flags { get; }
    public string Docs { get; }

    public bool Nullable => Flags.HasFlag("nullable");

    public static AttributeDef? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("name", out var nameEl) ||
            !element.TryGetProperty("type", out var typeEl))
        {
            return null;
        }

        return new AttributeDef(
            nameEl.GetString()!,
            WfnType.Parse(typeEl),
            element.TryGetProperty("values", out var v) ? Json.ToClr(v) as List<object?> ?? new() : new(),
            element.TryGetProperty("flags", out var f) ? JsonUtil.StringArray(f) : Array.Empty<string>(),
            element.TryGetProperty("docs", out var d) ? d.GetString() ?? "" : "");
    }

    public static List<AttributeDef> FromArray(JsonElement? array) => JsonUtil.MapArray(array, FromJson);
}
