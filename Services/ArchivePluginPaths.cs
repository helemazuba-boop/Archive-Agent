namespace ArchiveAgent.Services;

public sealed class ArchivePluginPaths
{
    public string PluginFolderPath { get; }

    public string PluginConfigFolder { get; }

    public string AssetsDirectory => Path.Combine(PluginFolderPath, "Assets_Archive");

    public string DataDirectory => Path.Combine(PluginConfigFolder, "data");

    public string LogsDirectory => Path.Combine(PluginConfigFolder, "logs");

    public string ConfigPath => Path.Combine(DataDirectory, "config.json");

    public string HostConfigPath => Path.Combine(DataDirectory, "host-config.json");

    public string StatePath => Path.Combine(DataDirectory, "state.json");

    public string ProcessSnapshotPath => Path.Combine(DataDirectory, ".engine-process.json");

    public string CoreScriptPath => Path.Combine(AssetsDirectory, "core.py");

    public string EmbeddedPythonPath => Path.Combine(AssetsDirectory, "python-embed", "python.exe");

    private ArchivePluginPaths(string pluginConfigFolder, string pluginFolderPath)
    {
        PluginConfigFolder = pluginConfigFolder;
        PluginFolderPath = pluginFolderPath;
    }

    public static ArchivePluginPaths CreatePrepared(string pluginConfigFolder, string pluginFolderPath)
    {
        var paths = new ArchivePluginPaths(pluginConfigFolder, pluginFolderPath);
        paths.Prepare();
        return paths;
    }

    private void Prepare()
    {
        Directory.CreateDirectory(PluginConfigFolder);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
