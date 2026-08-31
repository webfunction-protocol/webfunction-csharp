using System.Text.Json;

namespace WebFunction;

/// <summary>A machine-readable error code an endpoint (or the package as a whole) may return, with documentation.</summary>
public sealed class DocumentedError
{
    public DocumentedError(string code, string docs)
    {
        Code = code;
        Docs = docs;
    }

    public string Code { get; }
    public string Docs { get; }

    public static DocumentedError? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("code", out var codeEl))
        {
            return null;
        }

        return new DocumentedError(
            codeEl.GetString()!,
            element.TryGetProperty("docs", out var d) ? d.GetString() ?? "" : "");
    }

    public static List<DocumentedError> FromArray(JsonElement? array) => JsonUtil.MapArray(array, FromJson);
}
