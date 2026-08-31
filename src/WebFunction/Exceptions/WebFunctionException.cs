namespace WebFunction.Exceptions;

/// <summary>
/// Base type for every exception this library throws. Carries a machine-readable
/// <see cref="Code"/> alongside the usual <see cref="Exception.Message"/>, and optional
/// structured <see cref="Details"/> about what went wrong.
/// </summary>
public abstract class WebFunctionException : Exception
{
    protected WebFunctionException(string message, string? code = null, object? details = null)
        : base(message)
    {
        Code = code ?? DefaultCode(GetType().Name);
        Details = details;
    }

    /// <summary>A machine-readable error code. Defaults to a snake-cased, upper-cased form of the exception type name.</summary>
    public string Code { get; }

    /// <summary>Optional structured details about the error (e.g. the raw response body).</summary>
    public object? Details { get; }

    private static string DefaultCode(string typeName)
    {
        // "BadRequestException" -> "WFN_BAD_REQUEST_ERROR"
        var name = typeName.EndsWith("Exception", StringComparison.Ordinal)
            ? typeName[..^"Exception".Length]
            : typeName;

        var sb = new System.Text.StringBuilder("WFN_");
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(name[i]));
        }

        sb.Append("_ERROR");
        return sb.ToString();
    }
}

/// <summary>The server returned a 400 with a bad-request error (optionally a [code, message, details] triple).</summary>
public sealed class BadRequestException : WebFunctionException
{
    public BadRequestException(string message, string? code = null, object? details = null)
        : base(message, code, details)
    {
    }
}

/// <summary>The server returned a status code other than 200 or 400.</summary>
public sealed class UnexpectedStatusCodeException : WebFunctionException
{
    public UnexpectedStatusCodeException(string message, object? details = null)
        : base(message, details: details)
    {
    }
}

/// <summary>The response body was not valid JSON.</summary>
public sealed class JsonParseException : WebFunctionException
{
    public JsonParseException(string message, object? details = null)
        : base(message, details: details)
    {
    }
}

/// <summary>A <see cref="Promise"/>'s value was accessed before the owning pipeline resolved it.</summary>
public sealed class UnresolvedPromiseException : WebFunctionException
{
    public UnresolvedPromiseException()
        : base("Promise has not been resolved yet")
    {
    }
}
