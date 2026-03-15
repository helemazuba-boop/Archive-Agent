using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace ArchiveAgent.Services.Automations.Triggers;

/// <summary>
/// 归档操作失败触发器 - 继承 TriggerBase
/// </summary>
[TriggerInfo(ArchiveAutomationIds.OperationFailedTrigger, "归档操作执行失败时", "\uEA39")]
public sealed class ArchiveOperationFailedTrigger(ArchiveAutomationBridgeService automationBridge) : TriggerBase
{
    public override void Loaded()
    {
        automationBridge.OperationRunFailed += OnOperationRunFailed;
    }

    public override void UnLoaded()
    {
        automationBridge.OperationRunFailed -= OnOperationRunFailed;
    }

    private void OnOperationRunFailed(object? sender, ArchiveOperationRunEvent e)
    {
        Trigger();
    }
}
