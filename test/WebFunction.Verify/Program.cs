using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using WebFunction;
using WebFunction.Exceptions;

var failures = new List<string>();
var passed = 0;

async Task RunAsync(string name, Func<Task> test)
{
    try
    {
        await test();
        passed++;
        Console.WriteLine($"  PASS  {name}");
    }
    catch (Exception e)
    {
        failures.Add(name);
        Console.WriteLine($"  FAIL  {name}\n        {e}");
    }
}

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception($"assertion failed: {message}");
    }
}

var server = new MockServer();
server.Start();
Console.WriteLine($"Mock server listening on {server.BaseUrl}\n");

// --- 1. FromPackageEndpointAsync -> CallAsync round trip, PascalCase name mapping ------------

await RunAsync("FromPackageEndpoint -> Call round trip + name mapping", async () =>
{
    server.Handlers["/package"] = _ => Task.FromResult(JsonResponse(200, PackageJson(server.BaseUrl)));
    server.Handlers["/list-items"] = ctx =>
    {
        Check(ctx.Request.Headers["Content-Type"]?.StartsWith("application/json") == true, "content type header");
        return Task.FromResult(JsonResponse(200, "{\"items\":[1,2,3]}"));
    };

    dynamic client = await Client.FromPackageEndpointAsync($"{server.BaseUrl}/package");
    object? result = await client.ListItems(new Dictionary<string, object?>());

    Check(result is Dictionary<string, object?> d && d.ContainsKey("items"), "decoded result has items key");
});

// --- 2. Bad request error triple -> BadRequestException --------------------------------------

await RunAsync("bad-request error triple -> BadRequestException", async () =>
{
    server.Handlers["/package"] = _ => Task.FromResult(JsonResponse(200, PackageJson(server.BaseUrl)));
    server.Handlers["/list-items"] = _ =>
        Task.FromResult(JsonResponse(400, "[\"WFN_NOT_FOUND\",\"Not found\",{\"id\":\"abc\"}]"));

    dynamic client = await Client.FromPackageEndpointAsync($"{server.BaseUrl}/package");

    var thrown = await CatchAsync<BadRequestException>(async () => await client.ListItems(new Dictionary<string, object?>()));
    Check(thrown is not null, "BadRequestException was thrown");
    Check(thrown!.Code == "WFN_NOT_FOUND", $"code should be WFN_NOT_FOUND, was {thrown.Code}");
    Check(thrown.Message == "Not found", $"message should be 'Not found', was '{thrown.Message}'");
});

// --- 3. Pagination via the paginated flag -----------------------------------------------------

await RunAsync("pagination (paginated flag) - NextPage/PreviousPage navigation", async () =>
{
    server.Handlers["/package"] = _ => Task.FromResult(JsonResponse(200, PackageJson(server.BaseUrl)));
    var call = 0;
    server.Handlers["/list-paginated"] = _ =>
    {
        call++;
        return Task.FromResult(call switch
        {
            1 => JsonResponse(200, "{\"page\":[1,2],\"next\":{\"cursor\":\"n1\"},\"previous\":null}"),
            2 => JsonResponse(200, "{\"page\":[3,4],\"next\":null,\"previous\":{\"cursor\":\"p1\"}}"),
            _ => JsonResponse(200, "{\"page\":[1,2],\"next\":{\"cursor\":\"n1\"},\"previous\":null}"),
        });
    };

    dynamic client = await Client.FromPackageEndpointAsync($"{server.BaseUrl}/package");
    var page1 = await client.ListPaginated(new Dictionary<string, object?>()) as Page;
    Check(page1 is not null, "first call returns a Page");
    Check(page1!.Items.Count == 2, "page 1 has 2 items");
    Check(page1.HasNext, "page 1 has a next page");
    Check(!page1.HasPrevious, "page 1 has no previous page");

    var page2 = await page1.NextPageAsync();
    Check(page2 is not null, "next page fetched");
    Check(page2!.Items.Count == 2 && (long)page2.Items[0]! == 3, "page 2 has the right items");
    Check(page2.HasPrevious, "page 2 has a previous page");
    Check(!page2.HasNext, "page 2 has no next page");
});

// --- 4. Pipelining round trip: chained calls, promise field reference, resolve --------------

await RunAsync("pipelining round trip (chained calls, promise field reference)", async () =>
{
    server.Handlers["/package"] = _ => Task.FromResult(JsonResponse(200, PackageJsonWithPipeline(server.BaseUrl)));
    server.Handlers["/pipe"] = async ctx =>
    {
        var body = await new StreamReader(ctx.Request.InputStream).ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        var steps = doc.RootElement.GetProperty("steps");

        // A real pipeline endpoint resolves "$[n].field"-style references against earlier
        // results in the same batch server-side. This mock must do the same, or it silently
        // masks whether Promise-based argument passing actually works (the same lesson already
        // learned the hard way in webfunction-go's own pipelining test).
        var step0Result = new Dictionary<string, object?> { ["id"] = "u1" };
        var userIdArg = steps[1].GetProperty("body").GetProperty("user_id").GetString();
        var resolvedUserId = userIdArg == "$[0].id" ? (string)step0Result["id"]! : userIdArg;

        var results = new List<object?>
        {
            step0Result,
            new Dictionary<string, object?> { ["profile_for"] = resolvedUserId },
        };
        return JsonResponse(200, JsonSerializer.Serialize(results));
    };

    dynamic client = await Client.FromPackageEndpointAsync($"{server.BaseUrl}/package", pipelined: true);

    var userPromise = await client.CreateUser(new Dictionary<string, object?>()) as Promise;
    Check(userPromise is not null, "CreateUser under a pipelined client returns a Promise");

    var thrown = await CatchAsync<UnresolvedPromiseException>(() =>
    {
        _ = userPromise!.Value;
        return Task.CompletedTask;
    });
    Check(thrown is not null, "accessing .Value before pipeline execution throws UnresolvedPromiseException");

    var userIdRef = userPromise!.Field("id");
    var profilePromise = await client.GetProfile(new Dictionary<string, object?> { ["user_id"] = userIdRef }) as Promise;
    Check(profilePromise is not null, "GetProfile also returns a Promise");

    var profile = await profilePromise!.ResolveAsync();
    Check(userPromise.IsResolved, "first promise resolved as a side effect of pipeline execution");
    Check(profile is Dictionary<string, object?> pd && (string)pd["profile_for"]! == "u1",
        "second step's server-resolved reference used the first step's real id");
});

// --- 5. Type value validators ------------------------------------------------------------------

await RunAsync("WfnType.Valid() refinement checks", () =>
{
    using var emailType = JsonDocument.Parse("\"string.email\"");
    var email = WebFunction.Type.WfnType.Parse(emailType.RootElement);
    Check(email.Valid("a@b.com"), "valid email passes");
    Check(!email.Valid("not-an-email"), "invalid email fails");

    using var u32Type = JsonDocument.Parse("\"number.u32\"");
    var u32 = WebFunction.Type.WfnType.Parse(u32Type.RootElement);
    Check(u32.Valid(42L), "in-range u32 passes");
    Check(!u32.Valid(-1L), "negative value fails u32");

    return Task.CompletedTask;
});

// --- 6. FromUrlAsync (GET-based fetch, api_version query param) ------------------------------

await RunAsync("FromUrl (GET-based fetch, api_version query param)", async () =>
{
    server.Handlers["/plain-package"] = ctx =>
    {
        Check(ctx.Request.HttpMethod == "GET", "FromUrl uses GET");
        Check(ctx.Request.Url!.Query.Contains("api_version=v2"), "api_version sent as query param");
        return Task.FromResult(JsonResponse(200, PackageJson(server.BaseUrl)));
    };

    var client = await Client.FromUrlAsync($"{server.BaseUrl}/plain-package", version: "v2");
    Check(client.Package.BaseUrl == server.BaseUrl + "/", "package parsed correctly via FromUrl");
});

// --- 7. Package.ObjectInContext ------------------------------------------------------------

await RunAsync("Package.ObjectInContext", () =>
{
    using var doc = JsonDocument.Parse(PackageJsonWithObjects(server.BaseUrl));
    var package = WebFunction.Package.FromJson(doc.RootElement);

    var argCtx = package.ObjectInContext("address", ObjectContext.Arguments);
    Check(argCtx is not null, "address has members in the arguments context");

    var attrCtx = package.ObjectInContext("address", ObjectContext.Attributes);
    Check(attrCtx is null, "address has no members in the attributes context, treated as absent");

    return Task.CompletedTask;
});

// --- 8. Gzip-compressed response regression test --------------------------------------------

await RunAsync("gzip-compressed response is decoded correctly", async () =>
{
    server.Handlers["/package"] = _ => Task.FromResult(JsonResponse(200, PackageJson(server.BaseUrl)));
    server.Handlers["/list-items"] = _ => Task.FromResult(GzipJsonResponse(200, "{\"items\":[\"gz1\",\"gz2\"]}"));

    dynamic client = await Client.FromPackageEndpointAsync($"{server.BaseUrl}/package");
    object? result = await client.ListItems(new Dictionary<string, object?>());

    Check(result is Dictionary<string, object?> d &&
          d["items"] is List<object?> items && items.Count == 2 && (string)items[0]! == "gz1",
        "gzip response decompressed and parsed correctly");
});

server.Stop();

Console.WriteLine($"\n{passed} passed, {failures.Count} failed.");
if (failures.Count > 0)
{
    Environment.Exit(1);
}

// --- helpers -----------------------------------------------------------------------------------

static async Task<T?> CatchAsync<T>(Func<Task> action) where T : Exception
{
    try
    {
        await action();
        return null;
    }
    catch (T e)
    {
        return e;
    }
}

static MockResponse JsonResponse(int status, string body) =>
    new(status, Encoding.UTF8.GetBytes(body), gzip: false);

static MockResponse GzipJsonResponse(int status, string body)
{
    using var output = new MemoryStream();
    using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        gzip.Write(bytes, 0, bytes.Length);
    }

    return new MockResponse(status, output.ToArray(), gzip: true);
}

static string PackageJson(string baseUrl) => $$"""
{
  "base_url": "{{baseUrl}}/",
  "name": "test-package",
  "endpoints": [
    { "name": "list-items", "returns": ["object"], "arguments": [], "attributes": [] },
    { "name": "list-paginated", "returns": ["object"], "flags": ["paginated"], "arguments": [], "attributes": [] }
  ]
}
""";

static string PackageJsonWithPipeline(string baseUrl) => $$"""
{
  "base_url": "{{baseUrl}}/",
  "pipeline_url": "{{baseUrl}}/pipe",
  "name": "test-package",
  "endpoints": [
    { "name": "create-user", "returns": ["object"], "arguments": [], "attributes": [] },
    { "name": "get-profile", "returns": ["object"], "arguments": [], "attributes": [] }
  ]
}
""";

static string PackageJsonWithObjects(string baseUrl) => $$"""
{
  "base_url": "{{baseUrl}}/",
  "name": "test-package",
  "endpoints": [],
  "objects": [
    {
      "name": "address",
      "arguments": [ { "name": "street", "type": "string" } ],
      "attributes": []
    }
  ]
}
""";

internal sealed record MockResponse(int Status, byte[] Body, bool gzip);

internal sealed class MockServer
{
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;

    public Dictionary<string, Func<HttpListenerContext, Task<MockResponse>>> Handlers { get; } = new();

    public string BaseUrl { get; }

    public MockServer()
    {
        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
    }

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
    }

    private async Task LoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(token);
            }
            catch
            {
                return;
            }

            _ = HandleAsync(ctx);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        if (!Handlers.TryGetValue(path, out var handler))
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
            return;
        }

        MockResponse response;
        try
        {
            response = await handler(ctx);
        }
        catch (Exception e)
        {
            // A handler assertion failure must still produce a response — otherwise the client
            // just hangs until its own HTTP timeout, which masks the real failure as a timeout.
            Console.WriteLine($"      [mock server handler threw for {path}]: {e.Message}");
            response = new MockResponse(500, System.Text.Encoding.UTF8.GetBytes("{}"), gzip: false);
        }

        ctx.Response.StatusCode = response.Status;
        if (response.gzip)
        {
            ctx.Response.Headers.Add("Content-Encoding", "gzip");
        }

        ctx.Response.ContentType = "application/json";
        await ctx.Response.OutputStream.WriteAsync(response.Body);
        ctx.Response.Close();
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
