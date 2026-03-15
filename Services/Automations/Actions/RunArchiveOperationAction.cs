using ArchiveAgent.Models.Automations.Actions;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace ArchiveAgent.Services.Automations.Actions;

[ActionInfo(ArchiveAutomationIds.RunOperationAction, "执行归档操作", "\uE8E5")]
public sealed class RunArchiveOperationAction(
    ArchiveAutomationBridgeService automationBridge,
    ArchiveNotificationService notificationService,
    ArchiveOrchestrator orchestrator) : ActionBase<RunArchiveOperationActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var operation = ArchiveOrchestrator.NormalizeOperation(Settings.Operation);
        var targetPath = Settings.TargetPath?.Trim() ?? string.Empty;
        var result = await orchestrator.RunArchiveOperationAsync(operation, targetPath, requestSource: "automation");

        var runEvent = new ArchiveOperationRunEvent(
            DateTimeOffset.Now,
            result.Success,
            operation,
            targetPath,
            result.Message,
            false);
        automationBridge.PublishRunCompleted(runEvent);

        if (Settings.PublishCompletionNotification)
        {
            var title = result.Success ? "归档操作完成" : "归档操作失败";
            notificationService.Publish(title, result.Message);
        }

        if (!result.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message)
                ? "Archive operation failed."
                : result.Message);
        }
    }
}
