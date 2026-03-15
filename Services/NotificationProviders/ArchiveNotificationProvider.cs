using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls.NotificationTemplates;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Core.Models.Notification.Templates;
using NotificationRequest = ClassIsland.Core.Models.Notification.NotificationRequest;

namespace ArchiveAgent.Services.NotificationProviders;

/// <summary>
/// 归档通知提供者 - 继承 NotificationProviderBase
/// </summary>
[NotificationProviderInfo("A1B2C3D4-E5F6-7890-ABCD-EF1234567890", "Archive-Agent 通知", "\uE8E5", "显示 Archive-Agent 的归档通知")]
public sealed class ArchiveNotificationProvider : NotificationProviderBase
{
    public ArchiveNotificationProvider(ArchiveNotificationService notificationService)
    {
        notificationService.NotificationRequested += (_, e) => ShowArchiveNotification(e);
    }

    private void ShowArchiveNotification(ArchiveNotificationEvent e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var maskDuration = TimeSpan.FromSeconds(2);
            var overlayDuration = TimeSpan.FromSeconds(Math.Clamp(e.DurationSeconds, 1, 30));

            // Mask Phase - 入场横幅
            var maskContent = NotificationContent.CreateSimpleTextContent(e.PrimaryText, null);
            maskContent.Duration = maskDuration;
            maskContent.IsSpeechEnabled = false;

            // Overlay Phase - 停留提醒
            NotificationContent overlayContent;
            if (string.IsNullOrWhiteSpace(e.ScrollingText))
            {
                overlayContent = NotificationContent.CreateSimpleTextContent(e.PrimaryText, null);
            }
            else
            {
                var fullText = $"{e.PrimaryText}  {e.ScrollingText}";
                overlayContent = NotificationContent.CreateRollingTextContent(fullText, overlayDuration, 1, null);
            }
            overlayContent.Duration = overlayDuration;
            overlayContent.IsSpeechEnabled = false;

            var request = new NotificationRequest
            {
                MaskContent = maskContent,
                OverlayContent = overlayContent
            };

            ShowNotification(request);
        });
    }
}
