using System.Text.Json;
using ArchiveAgent.Models;

namespace ArchiveAgent.Services;

public interface IArchiveConfigManager
{
    ArchiveHostConfig Config { get; }

    void SaveConfig();

    event EventHandler<ArchiveHostConfig> ConfigChanged;
}

public sealed class ArchiveConfigManager : IArchiveConfigManager, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _configPath;
    private readonly object _configLock = new();
    private FileSystemWatcher? _watcher;
    private ArchiveHostConfig _config = new();
    private bool _disposed;

    public ArchiveHostConfig Config
    {
        get
        {
            lock (_configLock)
            {
                return _config;
            }
        }
    }

    public event EventHandler<ArchiveHostConfig>? ConfigChanged;

    public ArchiveConfigManager(ArchivePluginPaths pluginPaths)
    {
        Directory.CreateDirectory(pluginPaths.DataDirectory);
        _configPath = pluginPaths.HostConfigPath;
        LoadConfigInternal();
        InitializeWatcher(pluginPaths.DataDirectory);
    }

    public void SaveConfig()
    {
        lock (_configLock)
        {
            SaveConfigInternal();
        }
    }

    private void LoadConfigInternal()
    {
        lock (_configLock)
        {
            if (!File.Exists(_configPath))
            {
                _config = new ArchiveHostConfig();
                SaveConfigInternal();
            }
            else
            {
                try
                {
                    _config = JsonSerializer.Deserialize<ArchiveHostConfig>(File.ReadAllText(_configPath)) ?? new ArchiveHostConfig();
                }
                catch (Exception ex)
                {
                    ArchiveDiagnosticsLogger.Warn("ConfigManager", "Failed to read host config. Resetting to defaults.",
                        new { message = ex.Message, path = _configPath });
                    _config = new ArchiveHostConfig();
                    SaveConfigInternal();
                }
            }
        }

        ConfigChanged?.Invoke(this, _config);
    }

    private void SaveConfigInternal()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, JsonOptions));
    }

    private void InitializeWatcher(string dataDirectory)
    {
        try
        {
            _watcher = new FileSystemWatcher(dataDirectory, Path.GetFileName(_configPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            ArchiveDiagnosticsLogger.Warn("ConfigManager", "Failed to start host config watcher.", new { message = ex.Message });
        }
    }

    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        Task.Run(async () =>
        {
            await ReloadWithRetryAsync(LoadConfigInternal);
        });
    }

    private static async Task ReloadWithRetryAsync(Action reloadAction, int maxRetries = 5)
    {
        Exception? lastEx = null;
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                Thread.Sleep(50 << i);
                reloadAction();
                return;
            }
            catch (IOException ex)
            {
                lastEx = ex;
            }
        }
        ArchiveDiagnosticsLogger.Warn("ConfigManager", "Failed to reload config after retries.",
            new { retries = maxRetries, lastError = lastEx?.Message });
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
            _watcher.Changed -= OnConfigFileChanged;
            _watcher.Created -= OnConfigFileChanged;
            _watcher.Dispose();
        }
    }
}
