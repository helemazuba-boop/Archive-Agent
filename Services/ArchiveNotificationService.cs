namespace ArchiveAgent.Services;

/// <summary>
/// 归档通知服务 - 发布通知事件
/// </summary>
public sealed class ArchiveNotificationService
{
    private readonly ArchiveWindowsToastService? _toastService;

    public ArchiveNotificationService(ArchiveWindowsToastService? toastService = null)
    {
        _toastService = toastService;
    }

    public event EventHandler<ArchiveNotificationEvent>? NotificationRequested;

    public void Publish(string primaryText, string scrollingText = "", double durationSeconds = 8, string? undoTargetPath = null, string? undoOperation = null)
    {
        var primaryContent = (primaryText ?? string.Empty).Trim();
        var scrollingContent = (scrollingText ?? string.Empty).Trim();

        if (primaryContent.Length == 0 && scrollingContent.Length == 0)
        {
            return;
        }

        NotificationRequested?.Invoke(this, new ArchiveNotificationEvent(primaryContent, scrollingContent, durationSeconds));

        if (string.IsNullOrEmpty(undoTargetPath) || _toastService == null)
        {
            return;
        }

        try
        {
            _toastService.ShowToast("Archive-Agent", primaryContent, undoTargetPath, undoOperation);
        }
        catch
        {
        }
    }
}

/// <summary>
/// 归档通知事件
/// </summary>
public sealed record ArchiveNotificationEvent(
    string PrimaryText,
    string ScrollingText,
    double DurationSeconds);
