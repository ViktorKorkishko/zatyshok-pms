using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Zatyshok.Tests.Integration.Api;

/// <summary>One log record with the values of the scopes it was written in (e.g. CorrelationId).</summary>
internal sealed record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> ScopeValues);

/// <summary>
/// A logger provider that keeps the records in memory, for tests of what the API writes to the log.
/// It receives the scopes of the logger factory (ISupportExternalScope), like the console logger
/// with IncludeScopes, so a test sees the CorrelationId scope of CorrelationIdMiddleware.
/// </summary>
internal sealed class CapturedLogs : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _records = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<CapturedLog> Records => _records;

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class Logger(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = new Dictionary<string, object?>();
            owner._scopes.ForEachScope(
                (scope, collected) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            collected[key] = value;
                        }
                    }
                },
                values);

            owner._records.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), values));
        }
    }
}
