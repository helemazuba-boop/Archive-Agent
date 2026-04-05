namespace ArchiveAgent.Services;

public sealed class ArchivePluginLifecycle
{
    private readonly ArchiveOrchestrator _orchestrator;
    private readonly IArchivePythonIpcService _pythonIpcService;
    private readonly ArchiveFileWatchService _watchService;
    private readonly ArchiveScheduledTriggerService _scheduledTrigger;
    private readonly ArchiveWindowsToastService _toastService;
    private readonly ArchivePluginPaths _paths;
    private int _started;

    public ArchivePluginLifecycle(
        ArchiveOrchestrator orchestrator,
        IArchivePythonIpcService pythonIpcService,
        ArchiveFileWatchService watchService,
        ArchiveScheduledTriggerService scheduledTrigger,
        ArchiveWindowsToastService toastService,
        ArchivePluginPaths paths)
    {
        _orchestrator = orchestrator;
        _pythonIpcService = pythonIpcService;
        _watchService = watchService;
        _scheduledTrigger = scheduledTrigger;
        _toastService = toastService;
        _paths = paths;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        try
        {
            ArchivePythonProcessTracker.CleanupPersistedProcess(_paths.ProcessSnapshotPath);
            _orchestrator.StartRuntime();
            await Task.Run(() => _watchService.Initialize(cancellationToken), cancellationToken);
            _scheduledTrigger.Initialize();
            _toastService.Initialize(_pythonIpcService.BaseUrl);
        }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _watchService.StopWatching();
        _scheduledTrigger.Stop();
        _orchestrator.StopRuntime();
        await _pythonIpcService.StopAsync();
        ArchivePythonProcessTracker.CleanupTrackedProcesses();
    }
}
