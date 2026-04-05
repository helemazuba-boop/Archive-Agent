using System.Timers;
using ArchiveAgent.Models;
using Timer = System.Timers.Timer;

namespace ArchiveAgent.Services;

public sealed class ArchiveScheduledTriggerService : IDisposable
{
    private readonly ArchiveOrchestrator _orchestrator;
    private readonly ArchiveNotificationService _notificationService;
    private readonly ArchiveAutomationBridgeService _automationBridge;
    private readonly ArchiveFileOperationService _fileOperationService;
    private readonly ArchiveFileWatchService _watchService;
    private readonly Timer _timer;
    private bool _isProcessing;

    public ArchiveConfig Config { get; private set; } = new();

    public bool IsRunning => _timer.Enabled;

    public event EventHandler<string>? StatusChanged;

    public ArchiveScheduledTriggerService(
        ArchiveOrchestrator orchestrator,
        ArchiveNotificationService notificationService,
        ArchiveAutomationBridgeService automationBridge,
        ArchiveFileOperationService fileOperationService,
        ArchiveFileWatchService watchService)
    {
        _orchestrator = orchestrator;
        _notificationService = notificationService;
        _automationBridge = automationBridge;
        _fileOperationService = fileOperationService;
        _watchService = watchService;
        _timer = new Timer(IntervalMs(60))
        {
            AutoReset = false
        };
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Initialize()
    {
        LoadConfig();
        if (Config.ScheduledEnabled)
        {
            Start();
        }
    }

    public void LoadConfig()
    {
        Config = Task.Run(() => _orchestrator.LoadMergedConfigAsync("scheduled_trigger"))
            .GetAwaiter()
            .GetResult();
        UpdateTimerInterval();
        StatusChanged?.Invoke(this, $"Scheduled config loaded. Enabled={Config.ScheduledEnabled}, Interval={Config.ScheduledIntervalMinutes}min.");
    }

    public void SaveConfig()
    {
        try
        {
            _watchService.SaveConfig();
            LoadConfig();
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("ScheduledTrigger", "Failed to save config.", new { message = ex.Message });
        }
    }

    public void Start()
    {
        if (!Config.ScheduledEnabled)
        {
            StatusChanged?.Invoke(this, "Scheduled trigger is disabled in config.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Config.WatchPath) || !Directory.Exists(Config.WatchPath))
        {
            StatusChanged?.Invoke(this, $"Watch path does not exist: {Config.WatchPath}");
            return;
        }

        UpdateTimerInterval();
        _timer.Start();
        StatusChanged?.Invoke(this, $"Scheduled trigger started. Interval={Config.ScheduledIntervalMinutes}min.");
    }

    public void Stop()
    {
        _timer.Stop();
        StatusChanged?.Invoke(this, "Scheduled trigger stopped.");
    }

    public void Restart()
    {
        Stop();
        LoadConfig();
        if (Config.ScheduledEnabled)
        {
            Start();
        }
    }

    private void UpdateTimerInterval()
    {
        var minutes = Math.Max(1, Config.ScheduledIntervalMinutes);
        _timer.Interval = IntervalMs(minutes);
    }

    private static double IntervalMs(int minutes) => minutes * 60 * 1000.0;

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _ = RunScheduledScanAsync();
    }

    private async Task RunScheduledScanAsync()
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;
        try
        {
            LoadConfig();
            if (!Config.ScheduledEnabled)
            {
                StatusChanged?.Invoke(this, "Scheduled trigger disabled, skipping.");
                return;
            }

            var watchPath = Config.WatchPath;
            if (string.IsNullOrWhiteSpace(watchPath) || !Directory.Exists(watchPath))
            {
                StatusChanged?.Invoke(this, $"Watch path invalid: {watchPath}");
                return;
            }

            var status = $"Scheduled scan started at {watchPath}";
            StatusChanged?.Invoke(this, status);

            var files = ScanDirectory(watchPath);
            if (files.Count == 0)
            {
                StatusChanged?.Invoke(this, "No files to process in scheduled scan.");
                return;
            }

            var processed = 0;
            var failed = 0;

            foreach (var filePath in files)
            {
                try
                {
                    var result = await _orchestrator.RunArchiveOperationAsync(
                        Config.DefaultOperation,
                        filePath,
                        requestSource: "scheduled");

                    var processedFile = result.ProcessedFiles.FirstOrDefault();
                    if (processedFile?.Success == true)
                    {
                        processed++;
                        _automationBridge.PublishRunCompleted(new ArchiveOperationRunEvent(
                            DateTimeOffset.Now,
                            true,
                            ArchiveOrchestrator.NormalizeOperation(Config.DefaultOperation),
                            filePath,
                            processedFile.Message ?? "Success",
                            false));
                    }
                    else
                    {
                        failed++;
                        _automationBridge.PublishRunCompleted(new ArchiveOperationRunEvent(
                            DateTimeOffset.Now,
                            false,
                            ArchiveOrchestrator.NormalizeOperation(Config.DefaultOperation),
                            filePath,
                            processedFile?.Message ?? result.Message,
                            false));
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    ArchiveDiagnosticsLogger.Warn("ScheduledTrigger", "Failed to process file.",
                        new { filePath, message = ex.Message });
                }
            }

            var summary = $"Scheduled scan complete: {processed} processed, {failed} failed.";
            StatusChanged?.Invoke(this, summary);

            if (Config.ShowNotification)
            {
                _notificationService.Publish("Scheduled scan complete", summary);
            }
        }
        finally
        {
            _isProcessing = false;

            if (Config.ScheduledEnabled)
            {
                LoadConfig();
                UpdateTimerInterval();
                _timer.Start();
            }
        }
    }

    private static List<string> ScanDirectory(string path)
    {
        var result = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var _ = File.OpenRead(file);
                    result.Add(file);
                }
                catch (IOException)
                {
                }
            }
        }
        catch (Exception)
        {
        }
        return result;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
    }
}