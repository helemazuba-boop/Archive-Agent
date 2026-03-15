namespace ArchiveAgent.Services;

/// <summary>
/// 归档通知服务 - 发布通知事件
/// </summary>
public sealed class ArchiveNotificationService
{
    public event EventHandler<ArchiveNotificationEvent>? NotificationRequested;

    public void Publish(string primaryText, string scrollingText = "", double durationSeconds = 8)
    {
        var primaryContent = (primaryText ?? string.Empty).Trim();
        var scrollingContent = (scrollingText ?? string.Empty).Trim();

        if (primaryContent.Length == 0 && scrollingContent.Length == 0)
        {
            return;
        }

        NotificationRequested?.Invoke(this, new ArchiveNotificationEvent(primaryContent, scrollingContent, durationSeconds));
    }
}

/// <summary>
/// 归档通知事件
/// </summary>
public sealed record ArchiveNotificationEvent(
    string PrimaryText, 
    string ScrollingText, 
    double DurationSeconds);
