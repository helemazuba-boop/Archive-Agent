using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ArchiveAgent.Services;

internal static class ArchiveDiagnosticsLogger
{
    private static readonly object SyncRoot = new();
    private static readonly JsonSerializerOptions JsonOptions = new();
    private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private static string? _configuredLogDirectory;
    private static string _currentLogPath = BuildDefaultLogPath();
    private static bool _initialized;

    public static void Configure(string? logDirectory)
    {
        lock (SyncRoot)
        {
            _configuredLogDirectory = string.IsNullOrWhiteSpace(logDirectory) ? null : logDirectory;
            _initialized = false;
            _currentLogPath = BuildDefaultLogPath();
        }
    }

    public static void Info(string scope, string message, object? data = null)
    {
        Write("INFO", scope, message, data, null);
    }

    public static void Warn(string scope, string message, object? data = null)
    {
        Write("WARN", scope, message, data, null);
    }

    public static void Error(string scope, string message, Exception? ex = null, object? data = null)
    {
        Write("ERROR", scope, message, data, ex);
    }

    public static string CreateTraceId(string prefix = "archive")
    {
        var safePrefix = string.IsNullOrWhiteSpace(prefix) ? "archive" : Sanitize(prefix).ToLowerInvariant();
        return $"{safePrefix}-{Guid.NewGuid():N}";
    }

    private static void Write(string level, string scope, string message, object? data, Exception? ex)
    {
        try
        {
            lock (SyncRoot)
            {
                EnsureInitialized();
                var payloadText = SerializePayload(data);
                var exceptionText = ex is null ? string.Empty : $" ex=\"{Sanitize(ex.GetType().Name)}:{Sanitize(ex.Message)}\"";
                var line =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] [sid:{SessionId}] [pid:{Environment.ProcessId}] [tid:{Environment.CurrentManagedThreadId}] [{Sanitize(scope)}] {Sanitize(message)}{payloadText}{exceptionText}";
                File.AppendAllText(_currentLogPath, line + Environment.NewLine, new UTF8Encoding(false));
                Debug.WriteLine(line);
            }
        }
        catch (Exception logEx)
        {
            Debug.WriteLine($"ArchiveDiagnosticsLogger failed: {logEx}");
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        Directory.CreateDirectory(ResolveLogDirectory());
        _currentLogPath = BuildDefaultLogPath();
        _initialized = true;
    }

    private static string BuildDefaultLogPath()
    {
        return Path.Combine(ResolveLogDirectory(), $"archive-agent-{DateTime.Now:yyyyMMdd}.log");
    }

    private static string ResolveLogDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_configuredLogDirectory))
        {
            return _configuredLogDirectory;
        }

        try
        {
            var baseDir = Path.GetDirectoryName(typeof(ArchiveDiagnosticsLogger).Assembly.Location) ?? AppContext.BaseDirectory;
            return Path.Combine(baseDir, "Assets_Archive", "logs");
        }
        catch
        {
            return Path.Combine(AppContext.BaseDirectory, "Assets_Archive", "logs");
        }
    }

    private static string SerializePayload(object? data)
    {
        if (data is null)
        {
            return string.Empty;
        }

        try
        {
            return $" data={JsonSerializer.Serialize(data, JsonOptions)}";
        }
        catch (Exception ex)
        {
            return $" data=\"<serialize-failed:{Sanitize(ex.Message)}>\"";
        }
    }

    private static string Sanitize(string text)
    {
        return (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
