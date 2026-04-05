using System.Timers;
using ArchiveAgent.Models;
using Timer = System.Timers.Timer;

namespace ArchiveAgent.Services;

public sealed class ArchiveFileWatchService : IDisposable
{
    private readonly ArchiveNotificationService _notificationService;
    private readonly ArchiveAutomationBridgeService _automationBridge;
    private readonly ArchiveFileOperationService _fileOperationService;
    private readonly ArchiveOrchestrator _orchestrator;
    private readonly List<FileChangeEvent> _pendingChanges = [];
    private readonly object _lock = new();
    private FileSystemWatcher? _watcher;
    private readonly Timer _debounceTimer;
    private bool _isProcessing;

    public ArchiveConfig Config { get; private set; } = new();

    public event EventHandler<FileOrganizedEventArgs>? FileOrganized;

    public event EventHandler<FileChangeEvent>? FileChanged;

    public event EventHandler<string>? StatusChanged;

    public bool IsWatching => _watcher?.EnableRaisingEvents ?? false;

    public ArchiveFileWatchService(
        ArchiveNotificationService notificationService,
        ArchiveAutomationBridgeService automationBridge,
        ArchiveFileOperationService fileOperationService,
        ArchiveOrchestrator orchestrator)
    {
        _notificationService = notificationService;
        _automationBridge = automationBridge;
        _fileOperationService = fileOperationService;
        _orchestrator = orchestrator;
        _debounceTimer = new Timer(1000)
        {
            AutoReset = false
        };
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;
    }

    public void Initialize(CancellationToken cancellationToken = default)
    {
        LoadConfig();
        if (Config.IsWatchEnabled)
        {
            StartWatching();
        }
    }

    public void LoadConfig()
    {
        try
        {
            Config = Task.Run(() => _orchestrator.LoadMergedConfigAsync("watch_service"))
                .GetAwaiter()
                .GetResult();
            UpdateDebounceDelay();
            StatusChanged?.Invoke(this, "Configuration loaded.");
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("WatchService", "Failed to load merged config.", new { message = ex.Message });
            Config = new ArchiveConfig();
            UpdateDebounceDelay();
            StatusChanged?.Invoke(this, $"Failed to load configuration: {ex.Message}");
        }
    }

    public void SaveConfig()
    {
        try
        {
            var hostConfig = _orchestrator.HostConfig;
            hostConfig.WatchPath = Config.WatchPath?.Trim() ?? string.Empty;
            hostConfig.IsWatchEnabled = Config.IsWatchEnabled;
            hostConfig.ShowNotification = Config.ShowNotification;
            hostConfig.OpenExplorerAfterOperation = Config.OpenExplorerAfterOperation;
            hostConfig.DebounceDelay = Math.Max(100, Config.DebounceDelay);
            _orchestrator.SaveHostConfig();

            var backendPatch = ArchiveOrchestrator.CreateBackendPatch(Config);
            var backendConfig = Task.Run(() => _orchestrator.SaveBackendConfigAsync(backendPatch, "watch_service"))
                .GetAwaiter()
                .GetResult();
            Config = ArchiveOrchestrator.MergeConfig(hostConfig, backendConfig);
            UpdateDebounceDelay();
            StatusChanged?.Invoke(this, "Configuration saved.");
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("WatchService", "Failed to save merged config.", new { message = ex.Message });
            StatusChanged?.Invoke(this, $"Failed to save configuration: {ex.Message}");
        }
    }

    public void UpdateConfig(ArchiveConfig newConfig)
    {
        var wasWatching = IsWatching;
        StopWatching();
        Config = newConfig;
        SaveConfig();
        if (wasWatching && Config.IsWatchEnabled)
        {
            StartWatching();
        }
    }

    public void StartWatching()
    {
        if (string.IsNullOrWhiteSpace(Config.WatchPath) || !Directory.Exists(Config.WatchPath))
        {
            StatusChanged?.Invoke(this, $"Watch path does not exist: {Config.WatchPath}");
            return;
        }

        StopWatching();

        _watcher = new FileSystemWatcher(Config.WatchPath)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            Filter = "*.*"
        };

        _watcher.Created += OnFileCreated;
        _watcher.Changed += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.EnableRaisingEvents = true;

        StatusChanged?.Invoke(this, $"Watching: {Config.WatchPath}");
    }

    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileCreated;
            _watcher.Changed -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        StatusChanged?.Invoke(this, "Watching stopped.");
    }

    public void OpenFileLocation(string filePath)
    {
        _fileOperationService.OpenInExplorerAndSelect(filePath);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        QueueFileChange(e.FullPath, WatcherChangeTypes.Created);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (File.Exists(e.FullPath))
        {
            QueueFileChange(e.FullPath, WatcherChangeTypes.Changed);
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        QueueFileChange(e.FullPath, WatcherChangeTypes.Renamed);
    }

    private void QueueFileChange(string filePath, WatcherChangeTypes changeType)
    {
        lock (_lock)
        {
            if (_pendingChanges.Any(change => string.Equals(change.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            _pendingChanges.Add(new FileChangeEvent
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                ChangeType = changeType,
                Timestamp = DateTime.Now
            });

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    private void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _ = ProcessPendingChangesAsync();
    }

    private async Task ProcessPendingChangesAsync()
    {
        List<FileChangeEvent> changesToProcess;
        lock (_lock)
        {
            if (_pendingChanges.Count == 0)
            {
                return;
            }

            if (_isProcessing)
            {
                return;
            }

            changesToProcess = _pendingChanges.ToList();
            _pendingChanges.Clear();
        }

        _isProcessing = true;
        try
        {
            foreach (var change in changesToProcess)
            {
                FileChanged?.Invoke(this, change);
                await ProcessFileAsync(change).ConfigureAwait(false);
            }
        }
        finally
        {
            bool hasMore;
            lock (_lock)
            {
                hasMore = _pendingChanges.Count > 0;
                _isProcessing = false;
            }

            if (hasMore)
            {
                _ = ProcessPendingChangesAsync();
            }
        }
    }

    private async Task ProcessFileAsync(FileChangeEvent change)
    {
        string? targetPath = null;
        try
        {
            var result = await _orchestrator.RunArchiveOperationAsync(
                Config.DefaultOperation,
                change.FilePath,
                requestSource: "watcher");

            var processedFile = result.ProcessedFiles.FirstOrDefault(processed =>
                                    string.Equals(processed.SourcePath, change.FilePath, StringComparison.OrdinalIgnoreCase))
                                ?? result.ProcessedFiles.FirstOrDefault();

            if (processedFile != null)
            {
                change.TargetPath = processedFile.TargetPath;
                change.TargetDirectory = string.IsNullOrWhiteSpace(processedFile.TargetPath)
                    ? null
                    : Path.GetDirectoryName(processedFile.TargetPath);
                change.MatchedKeyword = processedFile.MatchedKeyword;
                targetPath = processedFile.TargetPath;
            }

            var success = result.Success && (processedFile?.Success ?? false);
            var message = processedFile?.Message ?? result.Message;
            var operation = ArchiveOrchestrator.NormalizeOperation(Config.DefaultOperation);

            _automationBridge.PublishRunCompleted(new ArchiveOperationRunEvent(
                DateTimeOffset.Now,
                success,
                operation,
                change.FilePath,
                message,
                true));

            if (success && Config.ShowNotification)
            {
                _notificationService.Publish(
                    "Archive complete",
                    message,
                    8,
                    targetPath,
                    ArchiveOrchestrator.NormalizeOperation(Config.DefaultOperation));
            }

            if (success && Config.OpenExplorerAfterOperation && !string.IsNullOrWhiteSpace(targetPath))
            {
                _fileOperationService.OpenInExplorerAndSelect(targetPath);
            }

            FileOrganized?.Invoke(this, new FileOrganizedEventArgs(change, targetPath, success, success ? null : message));
            StatusChanged?.Invoke(this, message);
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("WatchService", "Backend archive execution failed.",
                new { filePath = change.FilePath, message = ex.Message });

            _automationBridge.PublishRunCompleted(new ArchiveOperationRunEvent(
                DateTimeOffset.Now,
                false,
                ArchiveOrchestrator.NormalizeOperation(Config.DefaultOperation),
                change.FilePath,
                ex.Message,
                true));

            FileOrganized?.Invoke(this, new FileOrganizedEventArgs(change, targetPath, false, ex.Message));
            StatusChanged?.Invoke(this, $"Archive failed: {ex.Message}");
        }
    }

    private void UpdateDebounceDelay()
    {
        _debounceTimer.Interval = Math.Max(100, Config.DebounceDelay);
    }

    public void Dispose()
    {
        StopWatching();
        _debounceTimer.Dispose();
    }
}

public sealed class FileOrganizedEventArgs : EventArgs
{
    public FileChangeEvent ChangeEvent { get; }

    public string? TargetPath { get; }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public FileOrganizedEventArgs(FileChangeEvent changeEvent, string? targetPath, bool success, string? errorMessage = null)
    {
        ChangeEvent = changeEvent;
        TargetPath = targetPath;
        Success = success;
        ErrorMessage = errorMessage;
    }
}
