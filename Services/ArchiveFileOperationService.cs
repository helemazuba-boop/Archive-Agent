using System.Diagnostics;

namespace ArchiveAgent.Services;

/// <summary>
/// 文件操作服务 - 提供文件管理器操作
/// </summary>
public class ArchiveFileOperationService
{
    /// <summary>
    /// 打开文件管理器并选中指定文件（高亮显示）
    /// </summary>
    /// <param name="filePath">要选中的文件路径</param>
    public void OpenInExplorerAndSelect(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            // 使用 /select 参数打开资源管理器并选中文件
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                }
            };
            process.Start();
        }
        catch (Exception ex)
        {
            // 如果 /select 失败，尝试直接打开所在目录
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = directory,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
            catch
            {
                // 忽略错误
            }
        }
    }

    /// <summary>
    /// 打开指定目录
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    public void OpenDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch
        {
            // 忽略错误
        }
    }

    /// <summary>
    /// 批量打开多个文件所在位置
    /// </summary>
    /// <param name="filePaths">文件路径列表</param>
    public void OpenMultipleInExplorer(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            OpenInExplorerAndSelect(filePath);
            // 稍微延迟避免同时打开太多窗口
            Thread.Sleep(100);
        }
    }
}
