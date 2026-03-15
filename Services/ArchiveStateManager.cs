using System.Text.Json;
using ArchiveAgent.Models;

namespace ArchiveAgent.Services;

public interface IArchiveStateManager
{
    ArchiveState LoadState();

    void SaveState(ArchiveState state);

    event EventHandler<ArchiveState> StateChanged;
}

public sealed class ArchiveStateManager : IArchiveStateManager, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _statePath;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public event EventHandler<ArchiveState>? StateChanged;

    public ArchiveStateManager(ArchivePluginPaths pluginPaths)
    {
        Directory.CreateDirectory(pluginPaths.DataDirectory);
        _statePath = pluginPaths.StatePath;
        InitializeWatcher(pluginPaths.DataDirectory);
    }

    public ArchiveState LoadState()
    {
        _stateLock.Wait();
        try
        {
            return LoadStateUnsafe();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public void SaveState(ArchiveState state)
    {
        _stateLock.Wait();
        try
        {
            WriteStateUnsafe(state);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private ArchiveState LoadStateUnsafe()
    {
        if (!File.Exists(_statePath))
        {
            var emptyState = new ArchiveState();
            WriteStateUnsafe(emptyState);
            return emptyState;
        }

        try
        {
            return JsonSerializer.Deserialize<ArchiveState>(File.ReadAllText(_statePath)) ?? new ArchiveState();
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("StateManager", "Failed to read state file. Returning empty state.",
                new { message = ex.Message, path = _statePath });
            return new ArchiveState();
        }
    }

    private void WriteStateUnsafe(ArchiveState state)
    {
        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_statePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    private void InitializeWatcher(string dataDirectory)
    {
        try
        {
            _watcher = new FileSystemWatcher(dataDirectory, Path.GetFileName(_statePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            _watcher.Changed += OnStateFileChanged;
            _watcher.Created += OnStateFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("StateManager", "Failed to start state watcher.", new { message = ex.Message });
        }
    }

    private void OnStateFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        Task.Run(() =>
        {
            Thread.Sleep(50);
            StateChanged?.Invoke(this, LoadState());
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnStateFileChanged;
            _watcher.Created -= OnStateFileChanged;
            _watcher.Dispose();
        }

        _stateLock.Dispose();
    }
}
