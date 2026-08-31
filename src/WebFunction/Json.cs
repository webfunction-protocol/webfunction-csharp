using System.Text.Json;

namespace WebFunction;

/// <summary>
/// Shared JSON conversion helpers. Decoded values use plain CLR types
/// (<see cref="Dictionary{TKey,TValue}"/>, <see cref="List{T}"/>, <c>string</c>, <c>long</c>,
/// <c>double</c>, <c>bool</c>, <c>null</c>) rather than <see cref="JsonElement"/>, so callers can
/// navigate results without depending on System.Text.Json types directly — the closest C#
/// equivalent of Ruby's Hash/Array or Jackson's dynamic <c>Object</c> mapping in the Java client.
/// </summary>
public static class Json
{
    /// <summary>Converts a parsed <see cref="JsonElement"/> into a plain CLR object graph.</summary>
    public static object? ToClr(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToClr(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(ToClr).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    /// <summary>
    /// Serializes a CLR value (as produced by <see cref="ToClr"/>, plus request-argument shapes
    /// containing <see cref="Promise"/> instances) to a JSON string. An unresolved
    /// <see cref="Promise"/> embedded anywhere in the graph serializes as its JSONPath reference
    /// string, automatically, the same behavior as every other reference client.
    /// </summary>
    public static string Serialize(object? value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, value);
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case Promise promise:
                Write(writer, promise.IsResolved ? promise.Value : (object?)promise.Path.ToString());
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case System.Collections.IDictionary dict:
                writer.WriteStartObject();
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    writer.WritePropertyName(entry.Key.ToString()!);
                    Write(writer, entry.Value);
                }

                writer.WriteEndObject();
                break;
            case System.Collections.IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                throw new NotSupportedException($"Cannot serialize value of type {value.GetType()}");
        }
    }
}
