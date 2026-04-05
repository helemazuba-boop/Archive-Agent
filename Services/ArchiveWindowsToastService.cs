using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ArchiveAgent.Services;

public sealed class ArchiveWindowsToastService
{
    private static string? _baseUrl;

    public void Initialize(string baseUrl)
    {
        _baseUrl = baseUrl;
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            var args = ToastArguments.Parse(toastArgs.Argument);
            if (!args.TryGetValue("action", out var action) || action != "undo")
                return;

            args.TryGetValue("target", out var targetPath);
            args.TryGetValue("operation", out var operation);
            operation ??= "organize";

            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(_baseUrl))
                return;

            Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var payload = new { target_path = targetPath, operation = operation };
                    var content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json");
                    var response = await client.PostAsync($"{_baseUrl}/api/v1/archive/undo", content);
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<UndoResult>(json);

                    new ToastContentBuilder()
                        .AddText("撤销结果")
                        .AddText(result?.Success == true ? $"已撤销: {result?.Message}" : $"撤销失败: {result?.Message}")
                        .Show();
                }
                catch
                {
                    new ToastContentBuilder()
                        .AddText("撤销失败")
                        .AddText("无法连接后端服务")
                        .Show();
                }
            });
        };
    }

    public void ShowToast(string title, string message, string? undoTargetPath = null, string? undoOperation = null)
    {
        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .SetToastScenario(ToastScenario.Default);

        if (!string.IsNullOrEmpty(undoTargetPath))
        {
            builder.AddButton(new ToastButton()
                .SetContent("撤销")
                .AddArgument("action", "undo")
                .AddArgument("target", undoTargetPath)
                .AddArgument("operation", undoOperation ?? "organize"));
        }

        builder.Show();
    }

    private record UndoResult(bool Success, string Message);

    public void Dispose()
    {
        try
        {
            ToastNotificationManagerCompat.Uninstall();
        }
        catch
        {
        }
    }
}
