using System.Text.Json;

namespace WebFunction;

internal static class JsonUtil
{
    public static string[] StringArray(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToArray()
            : Array.Empty<string>();

    public static List<T> MapArray<T>(JsonElement? array, Func<JsonElement, T?> map) where T : class
    {
        var result = new List<T>();
        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var el in array.Value.EnumerateArray())
        {
            var mapped = map(el);
            if (mapped is not null)
            {
                result.Add(mapped);
            }
        }

        return result;
    }
}
