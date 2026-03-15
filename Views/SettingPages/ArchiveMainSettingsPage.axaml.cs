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

        Resources.Add("BoolToTextConverter", new BoolToTextConverter());
    }

    private void OnPageLoaded(object? sender, RoutedEventArgs e)
    {
        WatchService.LoadConfig();
        LoadConfig();
        ReloadHistory();
        UpdateWatchStatus();
    }

    private void LoadConfig()
    {
        var config = WatchService.Config;

        WatchPathBox.Text = config.WatchPath;
        EnableWatchToggle.IsChecked = config.IsWatchEnabled;
        ShowNotificationToggle.IsChecked = config.ShowNotification;
        OverwriteToggle.IsChecked = config.OverwriteExisting;
        OpenExplorerToggle.IsChecked = config.OpenExplorerAfterOperation;

        SimilarityThresholdSlider.Value = config.EmbeddingSimilarityThreshold;
        SimilarityThresholdText.Text = config.EmbeddingSimilarityThreshold.ToString("F1");
        EmbeddingApiEndpointBox.Text = config.EmbeddingApiEndpoint ?? string.Empty;
        EmbeddingApiKeyBox.Text = config.EmbeddingApiKey ?? string.Empty;
        EmbeddingModelNameBox.Text = config.EmbeddingModelName ?? string.Empty;

        EmbeddingProviderComboBox.SelectedIndex = config.EmbeddingProvider switch
        {
            "local" => 1,
            "remote" => 2,
            "hybrid" => 3,
            _ => 0
        };

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
        config.EmbeddingSimilarityThreshold = SimilarityThresholdSlider.Value;
        config.EmbeddingApiEndpoint = NormalizeOptionalText(EmbeddingApiEndpointBox.Text);
        config.EmbeddingApiKey = NormalizeOptionalText(EmbeddingApiKeyBox.Text);
        config.EmbeddingModelName = NormalizeOptionalText(EmbeddingModelNameBox.Text);
        config.EmbeddingProvider = (EmbeddingProviderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "keyword";

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
            Priority = (int)NewRulePriorityBox.Value
        });

        NewKeywordBox.Text = string.Empty;
        NewTargetDirBox.Text = string.Empty;
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
            HistoryItems.Add(new FileHistoryItem
            {
                Time = record.OccurredAt.LocalDateTime.ToString("MM-dd HH:mm:ss"),
                FileName = Path.GetFileName(record.SourcePath),
                MatchedRule = record.MatchedKeyword ?? "-",
                Status = record.Success ? "Success" : $"Failed: {record.Message}"
            });
        }

        HistoryEmptyStatePanel.IsVisible = HistoryItems.Count == 0;
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
        var pathText = string.IsNullOrWhiteSpace(watchPath) ? "Desktop" : Path.GetFileName(watchPath);

        StatusInfoBar.Message = $"Status: {statusText} | Path: {pathText}";
        StatusInfoBar.Severity = isWatching ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        ToggleWatchBtn.Content = CreateIconText(
            isWatching ? "\uE71B" : "\uE71A",
            isWatching ? "Stop watching" : "Start watching");
    }

    private void SetStatus(string message, IBrush? color)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = color == Brushes.Green
            ? InfoBarSeverity.Success
            : color == Brushes.Orange
                ? InfoBarSeverity.Warning
                : color == Brushes.Red
                    ? InfoBarSeverity.Error
                    : InfoBarSeverity.Informational;
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
}

public sealed class BoolToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var text = parameter?.ToString() ?? string.Empty;
        return value is true ? text : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
