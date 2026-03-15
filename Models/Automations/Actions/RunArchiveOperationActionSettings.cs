namespace ArchiveAgent.Models.Automations.Actions;

/// <summary>
/// 执行归档操作动作的设置
/// </summary>
public sealed class RunArchiveOperationActionSettings
{
    /// <summary>
    /// 操作类型（如：sort、move、copy、delete）
    /// </summary>
    public string Operation { get; set; } = "sort";

    /// <summary>
    /// 目标路径
    /// </summary>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否发布完成通知
    /// </summary>
    public bool PublishCompletionNotification { get; set; } = true;
}
