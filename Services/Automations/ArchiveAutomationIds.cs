namespace ArchiveAgent.Services;

/// <summary>
/// 自动化 ID 常量定义
/// </summary>
public static class ArchiveAutomationIds
{
    // 动作
    public const string RunOperationAction = "archive-agent.actions.runOperation";

    // 触发器
    public const string OperationSucceededTrigger = "archive-agent.triggers.operationSucceeded";
    public const string OperationFailedTrigger = "archive-agent.triggers.operationFailed";

    // 通知
    public const string OperationCompletedNotification = "archive-agent.operation.completed";
    public const string OperationSucceededNotification = "archive-agent.operation.succeeded";
    public const string OperationFailedNotification = "archive-agent.operation.failed";
}
