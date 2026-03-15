using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace ArchiveAgent.Services.Automations.Triggers;

/// <summary>
/// 归档操作成功触发器 - 继承 TriggerBase
/// </summary>
[TriggerInfo(ArchiveAutomationIds.OperationSucceededTrigger, "归档操作执行成功时", "\uE73E")]
public sealed class ArchiveOperationSucceededTrigger(ArchiveAutomationBridgeService automationBridge) : TriggerBase
{
    public override void Loaded()
    {
        automationBridge.OperationRunSucceeded += OnOperationRunSucceeded;
    }

    public override void UnLoaded()
    {
        automationBridge.OperationRunSucceeded -= OnOperationRunSucceeded;
    }

    private void OnOperationRunSucceeded(object? sender, ArchiveOperationRunEvent e)
    {
        Trigger();
    }
}
