using System.IO.Compression;
using System.Text;
using System.Text.Json;
using WebFunction.Exceptions;

namespace WebFunction;

/// <summary>
/// Low-level HTTP execution shared by <see cref="Client"/> and <see cref="Pipeline"/>. Builds the
/// standard headers, POSTs the JSON-encoded arguments, and maps the response to either a decoded
/// value or a typed exception. Deliberately does not do pagination wrapping itself — that is
/// driven by the calling endpoint's <c>paginated</c> flag, not response-shape sniffing (see
/// <see cref="Page"/>).
/// </summary>
internal static class RequestExecutor
{
    // A single shared HttpClient is the documented, idiomatic pattern (avoids socket exhaustion
    // from disposing a new HttpClientHandler per call). AutomaticDecompression is deliberately
    // left off: we set our own Accept-Encoding header below and handle gzip manually, so the
    // response's real Content-Encoding is always visible to us. This mirrors a real bug already
    // hit (and fixed) in the same spot in both webfunction-go and webfunction-java.
    private static readonly HttpClient HttpClient = new();

    public static Dictionary<string, string> BuildHeaders(string? bearerAuth, string? version)
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Accept"] = "application/json",
            ["User-Agent"] = $"webfunction-csharp/{Version.Current}",
            ["Accept-Encoding"] = "gzip",
        };

        if (!string.IsNullOrEmpty(bearerAuth))
        {
            headers["Authorization"] = $"Bearer {bearerAuth}";
        }

        if (!string.IsNullOrEmpty(version))
        {
            headers["Api-Version"] = version;
        }

        return headers;
    }

    /// <summary>Executes a POST of <paramref name="args"/> to <paramref name="url"/> and returns the decoded result.</summary>
    public static async Task<object?> ExecuteAsync(string url, string? bearerAuth, string? version, object? args)
    {
        var headers = BuildHeaders(bearerAuth, version);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        foreach (var (key, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        request.Content = new StringContent(Json.Serialize(args), Encoding.UTF8, "application/json");

        return await SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>Fetches <paramref name="url"/> via GET (used by <c>FromUrl</c>), returning the decoded package JSON.</summary>
    public static async Task<object?> GetAsync(string url, string? version)
    {
        var uri = version is null ? url : AddQueryParam(url, "api_version", version);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
        request.Headers.TryAddWithoutValidation("User-Agent", $"webfunction-csharp/{Version.Current}");

        return await SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<object?> SendAsync(HttpRequestMessage request)
    {
        using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
        var rawBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        var isGzip = response.Content.Headers.ContentEncoding.Any(e => string.Equals(e, "gzip", StringComparison.OrdinalIgnoreCase));
        var bodyBytes = isGzip ? Gunzip(rawBytes) : rawBytes;
        var body = Encoding.UTF8.GetString(bodyBytes);

        var statusCode = (int)response.StatusCode;
        if (statusCode != 200 && statusCode != 400)
        {
            throw new UnexpectedStatusCodeException(
                $"Unexpected status code ({statusCode})",
                new Dictionary<string, object?> { ["status_code"] = statusCode, ["raw_body"] = body });
        }

        JsonElement parsed;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "null" : body);
            parsed = doc.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new JsonParseException(e.Message,
                new Dictionary<string, object?> { ["status_code"] = statusCode, ["raw_body"] = body });
        }

        var result = Json.ToClr(parsed);

        if (statusCode == 400)
        {
            var code = "WFN_BAD_REQUEST_ERROR";
            var message = "Bad request";
            object? details = new Dictionary<string, object?> { ["body"] = result };

            if (result is List<object?> { Count: 3 } triple && triple[0] is string c && triple[1] is string m)
            {
                code = c;
                message = m;
                details = triple[2];
            }

            throw new BadRequestException(message, code, details);
        }

        return result;
    }

    private static byte[] Gunzip(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static string AddQueryParam(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
