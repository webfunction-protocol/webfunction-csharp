using WebFunction;
using WebFunction.Exceptions;

var token = Environment.GetEnvironmentVariable("RESERVEPAY_BEARER_TOKEN");
if (string.IsNullOrEmpty(token))
{
    Console.Error.WriteLine("RESERVEPAY_BEARER_TOKEN is not set.");
    Console.Error.WriteLine("Usage: RESERVEPAY_BEARER_TOKEN=... dotnet run");
    Environment.Exit(1);
    return;
}

try
{
    Console.WriteLine("Fetching package from https://api.reservepay.com/merchants ...");
    dynamic client = await Client.FromPackageEndpointAsync(
        "https://api.reservepay.com/merchants",
        bearerAuth: token);

    Console.WriteLine("Calling available-api-versions ...");
    object? result = await client.AvailableApiVersions(new Dictionary<string, object?>());

    Console.WriteLine("Result:");
    Console.WriteLine(WebFunction.Json.Serialize(result));
}
catch (WebFunctionException e)
{
    Console.Error.WriteLine($"WebFunction error [{e.Code}]: {e.Message}");
    if (e.Details is not null)
    {
        Console.Error.WriteLine($"Details: {WebFunction.Json.Serialize(e.Details)}");
    }

    Environment.Exit(1);
}
