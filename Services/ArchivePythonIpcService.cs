using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchiveAgent.Models;

namespace ArchiveAgent.Services;

public interface IArchivePythonIpcService : IDisposable
{
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task RestartEngineAsync();

    Task StopAsync();

    Task<ArchiveBackendConfig> GetBackendConfigAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<ArchiveBackendConfig> UpdateBackendConfigAsync(
        ArchiveBackendConfigPatch patch,
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<ArchiveBackendSnapshot> GetBackendSnapshotAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<ArchiveMatchResult> MatchFileAsync(
        ArchiveMatchRequest request,
        string requestSource = "watcher",
        string? traceId = null,
        CancellationToken cancellationToken = default);

    Task<ArchiveRunResult> RunArchiveAsync(
        ArchiveRunRequest request,
        string requestSource = "host",
        string? traceId = null,
        CancellationToken cancellationToken = default);

    bool IsReady { get; }

    ArchiveEngineState State { get; }

    string? LastErrorMessage { get; }
}

public enum ArchiveEngineState
{
    NotStarted,
    Initializing,
    Ready,
    Faulted
}

public sealed class ArchivePythonIpcService : IArchivePythonIpcService
{
    private const string TraceHeaderName = "X-Archive-Trace-Id";
    private const string RequestSourceHeaderName = "X-Archive-Request-Source";
    private const int StartupTimeoutSeconds = 15;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IArchiveConfigManager _configManager;
    private readonly ArchivePluginPaths _pluginPaths;
    private readonly HttpClient _httpClient;
    private readonly object _stateLock = new();
    private readonly StringBuilder _errorBuffer = new();
    private Process? _pythonProcess;
    private Task? _initializeTask;
    private TaskCompletionSource<int> _portTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ArchiveEngineState _state = ArchiveEngineState.NotStarted;
    private int _serverPort;
    private bool _disposed;
    private IntPtr _pythonJobHandle = IntPtr.Zero;

    public ArchivePythonIpcService(IArchiveConfigManager configManager, ArchivePluginPaths pluginPaths)
    {
        _configManager = configManager;
        _pluginPaths = pluginPaths;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public bool IsReady => State == ArchiveEngineState.Ready;

    public ArchiveEngineState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public string? LastErrorMessage { get; private set; }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (State == ArchiveEngineState.Ready)
        {
            return;
        }

        Task<int> waitTask;
        lock (_stateLock)
        {
            if (_state == ArchiveEngineState.Faulted)
            {
                throw new InvalidOperationException(LastErrorMessage ?? "Archive backend failed to start.");
            }

            waitTask = _portTcs.Task;
        }

        await EnsureStartedAsync();
        await waitTask.WaitAsync(cancellationToken);
    }

    public async Task RestartEngineAsync()
    {
        await StopAsync();
        lock (_stateLock)
        {
            _state = ArchiveEngineState.NotStarted;
            LastErrorMessage = null;
            if (_portTcs.Task.IsCompleted)
            {
                _portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        await EnsureReadyAsync();
    }

    public Task StopAsync()
    {
        ShutdownPythonServer();
        lock (_stateLock)
        {
            _state = ArchiveEngineState.NotStarted;
            LastErrorMessage = null;
            if (_portTcs.Task.IsCompleted)
            {
                _portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        return Task.CompletedTask;
    }

    public Task<ArchiveBackendConfig> GetBackendConfigAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ArchiveBackendConfig>(HttpMethod.Get, "/api/v1/config", null, requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveBackendConfig> UpdateBackendConfigAsync(
        ArchiveBackendConfigPatch patch,
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ArchiveBackendConfig>(HttpMethod.Patch, "/api/v1/config", patch, requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveBackendSnapshot> GetBackendSnapshotAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ArchiveBackendSnapshot>(HttpMethod.Get, "/api/v1/snapshot", null, requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveMatchResult> MatchFileAsync(
        ArchiveMatchRequest request,
        string requestSource = "watcher",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ArchiveMatchResult>(HttpMethod.Post, "/api/v1/archive/match", request, requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveRunResult> RunArchiveAsync(
        ArchiveRunRequest request,
        string requestSource = "host",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return SendJsonAsync<ArchiveRunResult>(HttpMethod.Post, "/api/v1/archive/run", request, requestSource, traceId, cancellationToken);
    }

    private Task EnsureStartedAsync()
    {
        lock (_stateLock)
        {
            if (_state == ArchiveEngineState.Ready)
            {
                return Task.CompletedTask;
            }

            if (_state == ArchiveEngineState.Initializing && _initializeTask != null)
            {
                return _initializeTask;
            }

            if (_state == ArchiveEngineState.Faulted)
            {
                return Task.CompletedTask;
            }

            _state = ArchiveEngineState.Initializing;
            if (_portTcs.Task.IsCompleted)
            {
                _portTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _initializeTask = InitializeBackgroundAsync();
            return _initializeTask;
        }
    }

    private async Task InitializeBackgroundAsync()
    {
        try
        {
            var pythonPath = ValidatePythonPath(
                _configManager.Config.PythonPath,
                _pluginPaths.PluginFolderPath,
                _pluginPaths.AssetsDirectory);
            if (!File.Exists(_pluginPaths.CoreScriptPath))
            {
                throw new FileNotFoundException($"Archive backend script was not found at {_pluginPaths.CoreScriptPath}.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{_pluginPaths.CoreScriptPath}\" --data-dir \"{_pluginPaths.DataDirectory}\" --server --port 0",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _pythonProcess = process;
            var startupPortTcs = _portTcs;

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                ArchiveDiagnosticsLogger.Info("PythonStdout", e.Data);
                if (e.Data.StartsWith("__ARCHIVE_SERVER_PORT__:", StringComparison.Ordinal) &&
                    int.TryParse(e.Data["__ARCHIVE_SERVER_PORT__:".Length..], out var port))
                {
                    lock (_stateLock)
                    {
                        if (!ReferenceEquals(_pythonProcess, process))
                        {
                            return;
                        }

                        _serverPort = port;
                    }

                    startupPortTcs.TrySetResult(port);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                {
                    return;
                }

                ArchiveDiagnosticsLogger.Warn("PythonStderr", e.Data);
                lock (_errorBuffer)
                {
                    if (_errorBuffer.Length < 16000)
                    {
                        _errorBuffer.AppendLine(e.Data);
                    }
                }
            };

            process.Exited += (_, _) =>
            {
                var exitCode = -1;
                try
                {
                    exitCode = process.ExitCode;
                }
                catch
                {
                }

                ArchivePythonProcessTracker.Unregister(process, _pluginPaths.ProcessSnapshotPath);

                lock (_stateLock)
                {
                    if (!ReferenceEquals(_pythonProcess, process))
                    {
                        return;
                    }

                    _state = ArchiveEngineState.Faulted;
                    LastErrorMessage = $"Archive backend exited unexpectedly with code {exitCode}.";
                    _serverPort = 0;
                }

                startupPortTcs.TrySetException(new InvalidOperationException(LastErrorMessage));
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start the archive backend process.");
            }

            EnsureProcessBoundToJob(process);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            ArchivePythonProcessTracker.Register(process, _pluginPaths.ProcessSnapshotPath);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(StartupTimeoutSeconds));
            using (cts.Token.Register(() =>
                       startupPortTcs.TrySetException(new TimeoutException($"Archive backend startup timed out ({StartupTimeoutSeconds}s)."))))
            {
                await startupPortTcs.Task;
            }

            lock (_stateLock)
            {
                _state = ArchiveEngineState.Ready;
            }

            lock (_errorBuffer)
            {
                _errorBuffer.Clear();
            }

            ArchiveDiagnosticsLogger.Info("PythonIpc", "Archive backend started.", new { port = _serverPort });
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _state = ArchiveEngineState.Faulted;
            }

            string pythonErrors;
            lock (_errorBuffer)
            {
                pythonErrors = _errorBuffer.ToString();
            }

            LastErrorMessage = string.IsNullOrWhiteSpace(pythonErrors)
                ? ex.Message
                : $"{ex.Message}{Environment.NewLine}--- Python Error ---{Environment.NewLine}{pythonErrors}";
            _portTcs.TrySetException(new InvalidOperationException(LastErrorMessage, ex));
            ArchiveDiagnosticsLogger.Error("PythonIpc", "Archive backend startup failed.", ex,
                new { message = LastErrorMessage });
            ShutdownPythonServer();
        }
        finally
        {
            lock (_stateLock)
            {
                _initializeTask = null;
            }
        }
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string relativePath,
        object? payload,
        string requestSource,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var effectiveTraceId = string.IsNullOrWhiteSpace(traceId)
            ? ArchiveDiagnosticsLogger.CreateTraceId("backend")
            : traceId.Trim();
        var effectiveRequestSource = string.IsNullOrWhiteSpace(requestSource) ? "host" : requestSource.Trim();

        await EnsureReadyAsync(cancellationToken);

        using var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_serverPort}{relativePath}");
        request.Headers.TryAddWithoutValidation(TraceHeaderName, effectiveTraceId);
        request.Headers.TryAddWithoutValidation(RequestSourceHeaderName, effectiveRequestSource);
        if (payload != null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        ArchiveDiagnosticsLogger.Info("PythonIpc", "Sending backend request.",
            new { traceId = effectiveTraceId, requestSource = effectiveRequestSource, method = method.Method, relativePath });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ArchiveDiagnosticsLogger.Warn("PythonIpc", "Backend request failed.",
                new
                {
                    traceId = effectiveTraceId,
                    requestSource = effectiveRequestSource,
                    method = method.Method,
                    relativePath,
                    statusCode = (int)response.StatusCode,
                    response = Truncate(responseText, 320)
                });
            response.EnsureSuccessStatusCode();
        }

        var parsed = JsonSerializer.Deserialize<T>(responseText, JsonOptions);
        if (parsed == null)
        {
            throw new InvalidOperationException($"Failed to parse backend response for {relativePath}.");
        }

        return parsed;
    }

    private void ShutdownPythonServer()
    {
        var process = _pythonProcess;
        if (process == null)
        {
            DisposeJobObject();
            return;
        }

        try
        {
            if (_serverPort > 0 && !process.HasExited)
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                client.PostAsync($"http://127.0.0.1:{_serverPort}/shutdown", null).Wait(2000);
            }
        }
        catch
        {
        }

        try
        {
            if (!process.HasExited && !process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
        finally
        {
            ArchivePythonProcessTracker.Unregister(process, _pluginPaths.ProcessSnapshotPath);
            process.Dispose();
            _pythonProcess = null;
            _serverPort = 0;
            DisposeJobObject();
        }
    }

    public static string ValidatePythonPath(string configuredPath, string pluginBasePath, string assetsBasePath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var resolvedPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(pluginBasePath, configuredPath);
            if (File.Exists(resolvedPath))
            {
                return resolvedPath;
            }
        }

        var embeddedPythonPath = Path.Combine(assetsBasePath, "python-embed", "python.exe");
        if (File.Exists(embeddedPythonPath))
        {
            return embeddedPythonPath;
        }

        return "python";
    }

    private static string Truncate(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private void EnsureProcessBoundToJob(Process process)
    {
        try
        {
            if (_pythonJobHandle == IntPtr.Zero)
            {
                _pythonJobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_pythonJobHandle == IntPtr.Zero)
                {
                    return;
                }

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                var ptr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, ptr, false);
                    if (!SetInformationJobObject(
                            _pythonJobHandle,
                            JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation,
                            ptr,
                            (uint)length))
                    {
                        CloseHandle(_pythonJobHandle);
                        _pythonJobHandle = IntPtr.Zero;
                        return;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            if (_pythonJobHandle != IntPtr.Zero)
            {
                AssignProcessToJobObject(_pythonJobHandle, process.Handle);
            }
        }
        catch
        {
        }
    }

    private void DisposeJobObject()
    {
        if (_pythonJobHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            CloseHandle(_pythonJobHandle);
        }
        catch
        {
        }
        finally
        {
            _pythonJobHandle = IntPtr.Zero;
        }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private enum JOBOBJECTINFOCLASS
    {
        JobObjectExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        JOBOBJECTINFOCLASS jobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        DisposeJobObject();
        _httpClient.Dispose();
    }
}
