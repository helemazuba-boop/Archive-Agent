using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using ArchiveAgent.Models;
using ArchiveAgent.Services;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;

namespace ArchiveAgent.Views.SettingPages;

[FullWidthPage]
[HidePageTitle]
[SettingsPageInfo("archive-agent.settings", "Archive-Agent", "\uE8E5", "\uE8E5")]
public partial class ArchiveMainSettingsPage : SettingsPageBase
{
    private ArchiveFileWatchService WatchService { get; } = IAppHost.GetService<ArchiveFileWatchService>();
    private IArchiveStateManager StateManager { get; } = IAppHost.GetService<IArchiveStateManager>();
    private ArchiveScheduledTriggerService ScheduledTrigger { get; } = IAppHost.GetService<ArchiveScheduledTriggerService>();
    private ArchiveOrchestrator Orchestrator { get; } = IAppHost.GetService<ArchiveOrchestrator>();

    public ObservableCollection<KeywordRule> Rules { get; } = [];

    public ObservableCollection<FileHistoryItem> HistoryItems { get; } = [];

    public ArchiveMainSettingsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;

        RulesListBox.ItemsSource = Rules;
        HistoryListBox.ItemsSource = HistoryItems;

        WatchService.StatusChanged += OnStatusChanged;
        StateManager.StateChanged += OnStateChanged;
        ScheduledTrigger.StatusChanged += OnScheduledStatusChanged;

        Resources.Add("BoolToTextConverter", new BoolToTextConverter());
        Resources.Add("MatchModeConverter", new MatchModeConverter());
    }

    private void OnPageLoaded(object? sender, RoutedEventArgs e)
    {
        WatchService.LoadConfig();
        LoadConfig();
        ReloadHistory();
        UpdateWatchStatus();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        WatchService.StatusChanged -= OnStatusChanged;
        StateManager.StateChanged -= OnStateChanged;
        ScheduledTrigger.StatusChanged -= OnScheduledStatusChanged;
    }

    private void LoadConfig()
    {
        var config = WatchService.Config;

        WatchPathBox.Text = config.WatchPath;
        EnableWatchToggle.IsChecked = config.IsWatchEnabled;
        ShowNotificationToggle.IsChecked = config.ShowNotification;
        OverwriteToggle.IsChecked = config.OverwriteExisting;
        OpenExplorerToggle.IsChecked = config.OpenExplorerAfterOperation;

        SimilarityThresholdSlider.Value = config.LlmFallbackThreshold;
        SimilarityThresholdText.Text = config.LlmFallbackThreshold.ToString("F1");
        EmbeddingApiEndpointBox.Text = config.LlmApiEndpoint ?? string.Empty;
        EmbeddingApiKeyBox.Text = config.LlmApiKey ?? string.Empty;
        EmbeddingModelNameBox.Text = config.LlmModelName ?? string.Empty;
        LlmEnabledToggle.IsChecked = config.LlmEnabled;
        LlmIncludeContentToggle.IsChecked = config.LlmIncludeContent;
        LlmContentMaxCharsBox.Value = config.LlmContentMaxChars;
        ScheduledEnabledToggle.IsChecked = config.ScheduledEnabled;
        ScheduledIntervalBox.Value = config.ScheduledIntervalMinutes;
        ScheduledIntervalBox.IsEnabled = config.ScheduledEnabled;
        SetLlmFieldsEnabled(config.LlmEnabled);

        Rules.Clear();
        foreach (var rule in config.KeywordRules)
        {
            Rules.Add(CloneRule(rule));
        }

        UpdateRulesEmptyState();
    }

    private void SaveConfig()
    {
        var config = WatchService.Config;

        config.WatchPath = WatchPathBox.Text?.Trim() ?? GetDefaultDesktopPath();
        config.IsWatchEnabled = EnableWatchToggle.IsChecked ?? true;
        config.ShowNotification = ShowNotificationToggle.IsChecked ?? true;
        config.OverwriteExisting = OverwriteToggle.IsChecked ?? false;
        config.OpenExplorerAfterOperation = OpenExplorerToggle.IsChecked ?? true;
        config.KeywordRules = Rules.Select(CloneRule).ToList();
        config.EmbeddingSimilarityThreshold = 0.7;
        config.EmbeddingApiEndpoint = null;
        config.EmbeddingApiKey = null;
        config.EmbeddingModelName = null;
        config.EmbeddingProvider = "keyword";
        config.LlmEnabled = LlmEnabledToggle.IsChecked ?? false;
        config.LlmApiEndpoint = NormalizeOptionalText(EmbeddingApiEndpointBox.Text);
        config.LlmApiKey = NormalizeOptionalText(EmbeddingApiKeyBox.Text);
        config.LlmModelName = NormalizeOptionalText(EmbeddingModelNameBox.Text);
        config.LlmFallbackThreshold = SimilarityThresholdSlider.Value;
        config.LlmIncludeContent = LlmIncludeContentToggle.IsChecked ?? false;
        config.LlmContentMaxChars = (int)(LlmContentMaxCharsBox.Value ?? 2048);
        config.ScheduledEnabled = ScheduledEnabledToggle.IsChecked ?? false;
        config.ScheduledIntervalMinutes = (int)(ScheduledIntervalBox.Value ?? 60);

        WatchService.SaveConfig();
    }

    private static string GetDefaultDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    private void OnLlmEnabledToggled(object? sender, RoutedEventArgs e)
    {
        var isEnabled = LlmEnabledToggle.IsChecked ?? false;
        SetLlmFieldsEnabled(isEnabled);
        SaveConfig();
    }

    private void OnLlmIncludeContentToggled(object? sender, RoutedEventArgs e)
    {
        SaveConfig();
    }

    private void OnScheduledEnabledToggled(object? sender, RoutedEventArgs e)
    {
        var isEnabled = ScheduledEnabledToggle.IsChecked ?? false;
        ScheduledIntervalBox.IsEnabled = isEnabled;
        SaveConfig();
        if (isEnabled)
        {
            ScheduledTrigger.Restart();
        }
        else
        {
            ScheduledTrigger.Stop();
        }
    }

    private void OnScheduledIntervalChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        SaveConfig();
        if (ScheduledEnabledToggle.IsChecked == true)
        {
            ScheduledTrigger.Restart();
        }
    }

    private void OnScheduledStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() => SetStatus($"[Schedule] {status}", Brushes.Gray));
    }

    private void OnLlmContentMaxCharsChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        SaveConfig();
    }

    private void SetLlmFieldsEnabled(bool isEnabled)
    {
        EmbeddingApiEndpointBox.IsEnabled = isEnabled;
        EmbeddingApiKeyBox.IsEnabled = isEnabled;
        EmbeddingModelNameBox.IsEnabled = isEnabled;
        SimilarityThresholdSlider.IsEnabled = isEnabled;
        LlmIncludeContentToggle.IsEnabled = isEnabled;
        LlmContentMaxCharsBox.IsEnabled = isEnabled;
    }

    private void OnToggleWatchClick(object? sender, RoutedEventArgs e)
    {
        if (WatchService.IsWatching)
        {
            WatchService.StopWatching();
        }
        else
        {
            SaveConfig();
            WatchService.StartWatching();
        }

        UpdateWatchStatus();
    }

    private void OnRefreshDataClick(object? sender, RoutedEventArgs e)
    {
        WatchService.LoadConfig();
        LoadConfig();
        ReloadHistory();
        UpdateWatchStatus();
        SetStatus("Configuration refreshed.", Brushes.Green);
    }

    private void OnAddRuleClick(object? sender, RoutedEventArgs e)
    {
        var keyword = (NewKeywordBox.Text ?? string.Empty).Trim();
        var targetDir = (NewTargetDirBox.Text ?? string.Empty).Trim();

        if (keyword.Length == 0)
        {
            SetStatus("Keyword is required.", Brushes.Orange);
            return;
        }

        if (targetDir.Length == 0)
        {
            SetStatus("Target directory is required.", Brushes.Orange);
            return;
        }

        Rules.Add(new KeywordRule
        {
            Keyword = keyword,
            TargetDirectory = targetDir,
            IsEnabled = true,
            MatchFileName = MatchFileNameCheckBox.IsChecked ?? true,
            MatchExtension = MatchExtensionCheckBox.IsChecked ?? false,
            MatchMode = (NewRuleMatchModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "word",
            Priority = (int)(NewRulePriorityBox.Value ?? 0)
        });

        NewKeywordBox.Text = string.Empty;
        NewTargetDirBox.Text = string.Empty;
        NewRuleMatchModeComboBox.SelectedIndex = 0;
        NewRulePriorityBox.Value = 0;

        UpdateRulesEmptyState();
        SaveConfig();
        SetStatus($"Rule added: {keyword}", Brushes.Green);
    }

    private void OnDeleteRuleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string ruleId)
        {
            return;
        }

        var rule = Rules.FirstOrDefault(item => item.Id == ruleId);
        if (rule == null)
        {
            return;
        }

        Rules.Remove(rule);
        UpdateRulesEmptyState();
        SaveConfig();
        SetStatus($"Rule removed: {rule.Keyword}", Brushes.Green);
    }

    private void OnConfigInputLostFocus(object? sender, RoutedEventArgs e)
    {
        SaveConfig();
    }

    private void OnConfigToggleChanged(object? sender, RoutedEventArgs e)
    {
        SaveConfig();
    }

    private void OnConfigSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SaveConfig();
    }

    private void OnSimilarityThresholdChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        SimilarityThresholdText.Text = SimilarityThresholdSlider.Value.ToString("F1");
        SaveConfig();
    }

    private void OnStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() => SetStatus(status, Brushes.Gray));
    }

    private void OnStateChanged(object? sender, ArchiveState state)
    {
        Dispatcher.UIThread.Post(() => ReloadHistory(state));
    }

    private void ReloadHistory()
    {
        ReloadHistory(StateManager.LoadState());
    }

    private void ReloadHistory(ArchiveState state)
    {
        HistoryItems.Clear();
        foreach (var record in state.OperationHistory
                     .OrderByDescending(item => item.OccurredAt)
                     .Take(100))
        {
            var sourcePath = record.SourcePath;
            var targetPath = record.TargetPath ?? string.Empty;
            var operation = record.Operation;
            var canUndo = record.Success
                && !string.IsNullOrEmpty(targetPath)
                && File.Exists(targetPath)
                && operation != "delete";

            HistoryItems.Add(new FileHistoryItem
            {
                Time = record.OccurredAt.LocalDateTime.ToString("MM-dd HH:mm:ss"),
                FileName = Path.GetFileName(sourcePath),
                MatchedRule = record.MatchedKeyword ?? "-",
                Status = record.Success ? "Success" : $"Failed: {record.Message}",
                TargetPath = targetPath,
                Operation = operation,
                CanUndo = canUndo
            });
        }

        HistoryEmptyStatePanel.IsVisible = HistoryItems.Count == 0;
    }

    private async void OnUndoHistoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string targetPath)
            return;

        var item = HistoryItems.FirstOrDefault(x => x.TargetPath == targetPath);
        if (item == null)
            return;

        try
        {
            var result = await Orchestrator.UndoOperationAsync(targetPath, item.Operation);
            if (result.Success)
            {
                SetStatus($"已撤销: {result.Message}", Brushes.Green);
            }
            else
            {
                SetStatus($"撤销失败: {result.Message}", Brushes.Orange);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"撤销异常: {ex.Message}", Brushes.OrangeRed);
        }

        ReloadHistory();
    }

    private void UpdateRulesEmptyState()
    {
        RulesEmptyStatePanel.IsVisible = Rules.Count == 0;
    }

    private void UpdateWatchStatus()
    {
        var isWatching = WatchService.IsWatching;
        var watchPath = WatchService.Config.WatchPath;
        var statusText = isWatching ? "Watching" : "Stopped";
        var pathText = string.IsNullOrWhiteSpace(watchPath)
            ? "Desktop"
            : GetDirectoryNameOrPath(watchPath);

        StatusInfoBar.Message = $"Status: {statusText} | Path: {pathText}";
        StatusInfoBar.Severity = isWatching ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        ToggleWatchBtn.Content = CreateIconText(
            isWatching ? "\uE71B" : "\uE71A",
            isWatching ? "Stop watching" : "Start watching");

        static string GetDirectoryNameOrPath(string path)
        {
            var name = Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? path : name;
        }
    }

    private static InfoBarSeverity ColorToSeverity(IBrush? color)
    {
        if (color is Avalonia.Media.SolidColorBrush scb)
        {
            var c = scb.Color;
            if (c.R > 200 && c.G > 150 && c.B < 50) return InfoBarSeverity.Warning;
            if (c.G > 150 && c.R < 50 && c.B < 50) return InfoBarSeverity.Success;
            if (c.R > 200 && c.G < 50 && c.B < 50) return InfoBarSeverity.Error;
        }
        return InfoBarSeverity.Informational;
    }

    private void SetStatus(string message, IBrush? color)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = ColorToSeverity(color);
    }

    private static KeywordRule CloneRule(KeywordRule rule)
    {
        return new KeywordRule
        {
            Id = rule.Id,
            Keyword = rule.Keyword,
            TargetDirectory = rule.TargetDirectory,
            IsEnabled = rule.IsEnabled,
            MatchFileName = rule.MatchFileName,
            MatchExtension = rule.MatchExtension,
            Priority = rule.Priority
        };
    }

    private static StackPanel CreateIconText(string icon, string text)
    {
        return new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = icon,
                    FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe Fluent Icons"),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = text,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                }
            }
        };
    }
}

public sealed class FileHistoryItem
{
    public string Time { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string MatchedRule { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string Operation { get; set; } = "organize";

    public bool CanUndo { get; set; } = true;
}

public sealed class BoolToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is true ? parameter?.ToString() ?? string.Empty : "-";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class MatchModeConverter : IValueConverter
{
    private static readonly Dictionary<string, string> ModeLabels = new()
    {
        ["word"] = "词",
        ["prefix"] = "前缀",
        ["suffix"] = "后缀",
        ["exact"] = "完整",
        ["substring"] = "子串",
    };

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not string mode)
        {
            return "词";
        }
        return ModeLabels.TryGetValue(mode, out var label) ? label : mode;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
