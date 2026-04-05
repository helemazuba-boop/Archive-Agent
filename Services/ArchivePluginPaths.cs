namespace ArchiveAgent.Services;

public sealed class ArchivePluginPaths
{
    public string PluginFolderPath { get; }

    public string PluginConfigFolder { get; }

    public string AssetsDirectory => Path.Combine(PluginFolderPath, "Assets_Archive");

    public string DataDirectory => Path.Combine(PluginConfigFolder, "data");

    public string LogsDirectory => Path.Combine(PluginConfigFolder, "logs");

    /// <summary>
    /// 主机端配置文件路径（实际使用）。
    /// </summary>
    public string HostConfigPath => Path.Combine(DataDirectory, "host-config.json");

    /// <summary>
    /// 归档配置路径（预留，供未来与 Python 后端配置合并使用）。
    /// </summary>
    [Obsolete("Use HostConfigPath instead. This path is reserved for future merged config support.")]
    public string ConfigPath => Path.Combine(DataDirectory, "config.json");

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
