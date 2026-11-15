using System.Text.RegularExpressions;

namespace Zatyshok.Api.Common.Logging;

/// <summary>
/// Gives every request a correlation id (table 9.9): taken from the
/// X-Correlation-Id request header, so that a client or nginx can pass its own, or created anew.
/// The id is returned in the same response header, added to every log record of the request as the
/// CorrelationId scope and to ProblemDetails errors; T4 writes it to the audit log (AuditLogs.CorrelationId).
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>Name of the request and response header.</summary>
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "CorrelationId";

    /// <summary>Processes one request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadOrCreate(context.Request);
        context.Items[ItemKey] = correlationId;

        // Set when the response starts: the exception handler clears the headers set earlier.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // A scope is shared by all loggers, so every record written while the request runs gets it.
        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(context);
        }
    }

    /// <summary>Correlation id of the current request, or null outside a request.</summary>
    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;

    private static string ReadOrCreate(HttpRequest request)
    {
        // The value goes into logs and the audit log, so only a short id of safe characters is
        // accepted; anything else (a line break, a long text) is replaced with a new id.
        var fromClient = request.Headers[HeaderName].ToString();
        return SafeId().IsMatch(fromClient) ? fromClient : Guid.NewGuid().ToString();
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,64}$")]
    private static partial Regex SafeId();
}
