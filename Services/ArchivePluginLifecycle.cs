namespace ArchiveAgent.Services;

public sealed class ArchivePluginLifecycle
{
    private readonly ArchiveOrchestrator _orchestrator;
    private readonly IArchivePythonIpcService _pythonIpcService;
    private readonly ArchiveFileWatchService _watchService;
    private readonly ArchivePluginPaths _paths;
    private int _started;

    public ArchivePluginLifecycle(
        ArchiveOrchestrator orchestrator,
        IArchivePythonIpcService pythonIpcService,
        ArchiveFileWatchService watchService,
        ArchivePluginPaths paths)
    {
        _orchestrator = orchestrator;
        _pythonIpcService = pythonIpcService;
        _watchService = watchService;
        _paths = paths;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return Task.CompletedTask;
        }

        ArchivePythonProcessTracker.CleanupPersistedProcess(_paths.ProcessSnapshotPath);
        _orchestrator.StartRuntime();
        _watchService.Initialize();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
        {
            return;
        }

        _watchService.StopWatching();
        _orchestrator.StopRuntime();
        await _pythonIpcService.StopAsync();
        ArchivePythonProcessTracker.CleanupTrackedProcesses();
    }
}
