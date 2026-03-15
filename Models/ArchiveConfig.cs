using System.Collections.ObjectModel;

namespace ArchiveAgent.Models;

/// <summary>
/// 关键字整理规则
/// </summary>
public class KeywordRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Keyword { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool MatchFileName { get; set; } = true;
    public bool MatchExtension { get; set; } = false;
    public int Priority { get; set; } = 0;
}

/// <summary>
/// 归档配置
/// </summary>
public class ArchiveConfig
{
    /// <summary>
    /// 监控路径（默认桌面）
    /// </summary>
    public string WatchPath { get; set; } = GetDefaultDesktopPath();

    /// <summary>
    /// 是否启用自动监控
    /// </summary>
    public bool IsWatchEnabled { get; set; } = true;

    /// <summary>
    /// 是否显示通知
    /// </summary>
    public bool ShowNotification { get; set; } = true;

    /// <summary>
    /// 操作后打开文件管理器
    /// </summary>
    public bool OpenExplorerAfterOperation { get; set; } = true;

    /// <summary>
    /// 默认操作类型
    /// </summary>
    public string DefaultOperation { get; set; } = "organize";

    /// <summary>
    /// 是否覆盖现有文件
    /// </summary>
    public bool OverwriteExisting { get; set; } = false;

    /// <summary>
    /// 关键字规则列表
    /// </summary>
    public List<KeywordRule> KeywordRules { get; set; } = new();

    /// <summary>
    /// 自动归档频率
    /// </summary>
    public string AutoArchiveFrequency { get; set; } = "realtime";

    /// <summary>
    /// 防抖延迟（毫秒）
    /// </summary>
    public int DebounceDelay { get; set; } = 1000;

    #region 嵌入模型配置（预留）

    /// <summary>
    /// 嵌入服务提供者类型
    /// </summary>
    public string EmbeddingProvider { get; set; } = "keyword";

    /// <summary>
    /// 本地模型路径
    /// </summary>
    public string? EmbeddingModelPath { get; set; }

    /// <summary>
    /// 远程 API 端点
    /// </summary>
    public string? EmbeddingApiEndpoint { get; set; }

    /// <summary>
    /// API Key
    /// </summary>
    public string? EmbeddingApiKey { get; set; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string? EmbeddingModelName { get; set; }

    /// <summary>
    /// 相似度阈值
    /// </summary>
    public double EmbeddingSimilarityThreshold { get; set; } = 0.7;

    #endregion

    private static string GetDefaultDesktopPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
    }
}

/// <summary>
/// 文件变化事件
/// </summary>
public class FileChangeEvent
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public WatcherChangeTypes ChangeType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? MatchedKeyword { get; set; }
    public string? TargetDirectory { get; set; }
    public string? TargetPath { get; set; } // 操作后的目标路径
}
