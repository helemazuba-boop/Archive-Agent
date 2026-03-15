namespace ArchiveAgent.Models;

public sealed class ArchiveHostConfig
{
    public string WatchPath { get; set; } = GetDefaultDesktopPath();

    public bool IsWatchEnabled { get; set; } = true;

    public bool ShowNotification { get; set; } = true;

    public bool OpenExplorerAfterOperation { get; set; } = true;

    public int DebounceDelay { get; set; } = 1000;

    public string PythonPath { get; set; } = string.Empty;

    private static string GetDefaultDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }
}
