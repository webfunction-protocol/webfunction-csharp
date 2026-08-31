using System.Text.Json;
using WebFunction.Type;

namespace WebFunction;

/// <summary>An endpoint request parameter, as declared under a package's <c>arguments</c> key.</summary>
public sealed class Argument
{
    public Argument(string name, WfnType type, string? group, IReadOnlyList<object?> choices, IReadOnlyList<string> flags, string docs)
    {
        Name = name;
        ArgType = type;
        Group = group;
        Choices = choices;
        Flags = flags;
        Docs = docs;
    }

    public string Name { get; }
    public WfnType ArgType { get; }
    public string? Group { get; }
    public IReadOnlyList<object?> Choices { get; }
    public IReadOnlyList<string> Flags { get; }
    public string Docs { get; }

    public bool Required => Flags.HasFlag("required");
    public bool Optional => !Required;

    public static Argument? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("name", out var nameEl) ||
            !element.TryGetProperty("type", out var typeEl))
        {
            return null;
        }

        return new Argument(
            nameEl.GetString()!,
            WfnType.Parse(typeEl),
            element.TryGetProperty("group", out var g) ? g.GetString() : null,
            element.TryGetProperty("choices", out var c) ? Json.ToClr(c) as List<object?> ?? new() : new(),
            element.TryGetProperty("flags", out var f) ? JsonUtil.StringArray(f) : Array.Empty<string>(),
            element.TryGetProperty("docs", out var d) ? d.GetString() ?? "" : "");
    }

    public static List<Argument> FromArray(JsonElement? array) => JsonUtil.MapArray(array, FromJson);
}
