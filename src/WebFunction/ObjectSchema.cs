using System.Text.Json;

namespace WebFunction;

/// <summary>The context an <c>object.&lt;name&gt;</c> reference appears in, selecting which member set applies.</summary>
public enum ObjectContext
{
    Arguments,
    Attributes,
}

/// <summary>
/// A named object definition, declared under a package's <c>objects</c> key and referenced as a
/// refined object type (<c>object.&lt;name&gt;</c>) anywhere a type is expected. Named
/// <c>ObjectSchema</c> rather than <c>Object</c> to avoid clashing with <see cref="object"/>,
/// mirroring the same naming decision already made in the Ruby, Go, and Java clients.
/// </summary>
public sealed class ObjectSchema
{
    private readonly Dictionary<string, Argument> _arguments;
    private readonly Dictionary<string, AttributeDef> _attributes;

    public ObjectSchema(string name, IReadOnlyList<Argument> arguments, IReadOnlyList<AttributeDef> attributes)
    {
        Name = name;
        _arguments = arguments.ToDictionary(a => a.Name);
        _attributes = attributes.ToDictionary(a => a.Name);
    }

    public string Name { get; }

    public IReadOnlyList<Argument> Arguments => _arguments.Values.ToList();

    public Argument? GetArgument(string name) => _arguments.GetValueOrDefault(name);

    public IReadOnlyList<AttributeDef> Attributes => _attributes.Values.ToList();

    public AttributeDef? GetAttribute(string name) => _attributes.GetValueOrDefault(name);

    /// <summary>The object's properties for the given context (arguments or attributes), as a plain object list.</summary>
    public IReadOnlyList<object> Properties(ObjectContext context) => context switch
    {
        ObjectContext.Arguments => Arguments.Cast<object>().ToList(),
        ObjectContext.Attributes => Attributes.Cast<object>().ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(context)),
    };

    public static ObjectSchema? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("name", out var nameEl))
        {
            return null;
        }

        return new ObjectSchema(
            nameEl.GetString()!,
            element.TryGetProperty("arguments", out var a) ? Argument.FromArray(a) : new(),
            element.TryGetProperty("attributes", out var attrs) ? AttributeDef.FromArray(attrs) : new());
    }

    public static List<ObjectSchema> FromArray(JsonElement? array) => JsonUtil.MapArray(array, FromJson);
}
