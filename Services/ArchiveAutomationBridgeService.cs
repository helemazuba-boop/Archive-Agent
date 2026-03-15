using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveAgent.Services;

/// <summary>
/// 归档自动化桥接服务 - 发布触发事件
/// </summary>
public sealed class ArchiveAutomationBridgeService
{
    private readonly IIpcService? _ipcService;
    private readonly IRulesetService? _rulesetService;

    public event EventHandler<ArchiveOperationRunEvent>? OperationRunCompleted;
    public event EventHandler<ArchiveOperationRunEvent>? OperationRunSucceeded;
    public event EventHandler<ArchiveOperationRunEvent>? OperationRunFailed;

    public ArchiveOperationRunEvent? LastRunEvent { get; private set; }

    public ArchiveAutomationBridgeService(IServiceProvider serviceProvider)
    {
        _ipcService = serviceProvider.GetService<IIpcService>();
        _rulesetService = serviceProvider.GetService<IRulesetService>();
    }

    public void PublishRunCompleted(ArchiveOperationRunEvent runEvent)
    {
        LastRunEvent = runEvent;
        OperationRunCompleted?.Invoke(this, runEvent);

        if (runEvent.Success)
        {
            OperationRunSucceeded?.Invoke(this, runEvent);
        }
        else
        {
            OperationRunFailed?.Invoke(this, runEvent);
        }

        NotifyRulesetStatusChanged();
        _ = BroadcastAsync(ArchiveAutomationIds.OperationCompletedNotification, runEvent);
        _ = BroadcastAsync(
            runEvent.Success
                ? ArchiveAutomationIds.OperationSucceededNotification
                : ArchiveAutomationIds.OperationFailedNotification,
            runEvent);
    }

    private void NotifyRulesetStatusChanged()
    {
        if (_rulesetService == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(_rulesetService.NotifyStatusChanged);
    }

    private async Task BroadcastAsync<T>(string id, T payload) where T : class
    {
        if (_ipcService == null)
        {
            return;
        }

        try
        {
            await _ipcService.BroadcastNotificationAsync(id, payload);
        }
        catch
        {
            // 忽略广播错误
        }
    }
}

/// <summary>
/// 归档操作运行事件
/// </summary>
public sealed record ArchiveOperationRunEvent(
    DateTimeOffset OccurredAt,
    bool Success,
    string Operation,
    string TargetPath,
    string Message,
    bool IsAutoRun = false);
