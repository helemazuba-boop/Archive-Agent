using ArchiveAgent.Models;

namespace ArchiveAgent.Services;

public sealed class ArchiveOrchestrator
{
    private readonly IArchiveConfigManager _configManager;
    private readonly IArchiveStateManager _stateManager;
    private readonly IArchivePythonIpcService _pythonIpcService;
    private int _runtimeStarted;

    public ArchiveOrchestrator(
        IArchiveConfigManager configManager,
        IArchiveStateManager stateManager,
        IArchivePythonIpcService pythonIpcService)
    {
        _configManager = configManager;
        _stateManager = stateManager;
        _pythonIpcService = pythonIpcService;
    }

    public ArchiveHostConfig HostConfig => _configManager.Config;

    public ArchiveState LoadState() => _stateManager.LoadState();

    public async Task<ArchiveConfig> LoadMergedConfigAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var backendConfig = await _pythonIpcService.GetBackendConfigAsync(requestSource, traceId, cancellationToken);
        return MergeConfig(_configManager.Config, backendConfig);
    }

    public void SaveHostConfig()
    {
        _configManager.SaveConfig();
    }

    public Task<ArchiveBackendConfig> LoadBackendConfigAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return _pythonIpcService.GetBackendConfigAsync(requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveBackendConfig> SaveBackendConfigAsync(
        ArchiveBackendConfigPatch patch,
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return _pythonIpcService.UpdateBackendConfigAsync(patch, requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveBackendSnapshot> LoadBackendSnapshotAsync(
        string requestSource = "host_settings",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return _pythonIpcService.GetBackendSnapshotAsync(requestSource, traceId, cancellationToken);
    }

    public Task<ArchiveMatchResult> MatchFileAsync(
        string filePath,
        string requestSource = "watcher",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return _pythonIpcService.MatchFileAsync(
            new ArchiveMatchRequest { FilePath = filePath },
            requestSource,
            traceId,
            cancellationToken);
    }

    public Task<ArchiveRunResult> RunArchiveOperationAsync(
        string operation,
        string targetPath,
        string requestSource = "host",
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        return _pythonIpcService.RunArchiveAsync(
            new ArchiveRunRequest
            {
                Operation = NormalizeOperation(operation),
                TargetPath = targetPath?.Trim() ?? string.Empty
            },
            requestSource,
            traceId,
            cancellationToken);
    }

    public Task<ArchiveUndoResult> UndoOperationAsync(
        string targetPath,
        string operation = "organize",
        CancellationToken cancellationToken = default)
    {
        return _pythonIpcService.UndoOperationAsync(
            new ArchiveUndoRequest { TargetPath = targetPath, Operation = operation },
            "undo",
            null,
            cancellationToken);
    }

    public void StartRuntime()
    {
        if (Interlocked.Exchange(ref _runtimeStarted, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _pythonIpcService.EnsureReadyAsync();
                ArchiveDiagnosticsLogger.Info("Orchestrator", "Python backend is ready.");
            }
            catch (Exception ex)
            {
                ArchiveDiagnosticsLogger.Warn("Orchestrator", "Python backend failed to start during warmup.",
                    new { message = ex.Message });
            }
        });
    }

    public void StopRuntime()
    {
        Interlocked.Exchange(ref _runtimeStarted, 0);
    }

    public static ArchiveConfig MergeConfig(ArchiveHostConfig hostConfig, ArchiveBackendConfig backendConfig)
    {
        return new ArchiveConfig
        {
            WatchPath = hostConfig.WatchPath,
            IsWatchEnabled = hostConfig.IsWatchEnabled,
            ShowNotification = hostConfig.ShowNotification,
            OpenExplorerAfterOperation = hostConfig.OpenExplorerAfterOperation,
            DebounceDelay = hostConfig.DebounceDelay,
            DefaultOperation = backendConfig.DefaultOperation,
            OverwriteExisting = backendConfig.OverwriteExisting,
            KeywordRules = backendConfig.KeywordRules.Select(CloneRule).ToList(),
            EmbeddingProvider = backendConfig.EmbeddingProvider,
            EmbeddingModelPath = backendConfig.EmbeddingModelPath,
            EmbeddingApiEndpoint = backendConfig.EmbeddingApiEndpoint,
            EmbeddingApiKey = backendConfig.EmbeddingApiKey,
            EmbeddingModelName = backendConfig.EmbeddingModelName,
            EmbeddingSimilarityThreshold = backendConfig.EmbeddingSimilarityThreshold,
            LlmEnabled = backendConfig.LlmEnabled,
            LlmApiEndpoint = backendConfig.LlmApiEndpoint,
            LlmApiKey = backendConfig.LlmApiKey,
            LlmModelName = backendConfig.LlmModelName,
            LlmFallbackThreshold = backendConfig.LlmFallbackThreshold,
            LlmIncludeContent = backendConfig.LlmIncludeContent,
            LlmContentMaxChars = backendConfig.LlmContentMaxChars,
            ScheduledEnabled = backendConfig.ScheduledEnabled,
            ScheduledIntervalMinutes = backendConfig.ScheduledIntervalMinutes,
        };
    }

    public static ArchiveBackendConfigPatch CreateBackendPatch(ArchiveConfig config)
    {
        return new ArchiveBackendConfigPatch
        {
            DefaultOperation = NormalizeOperation(config.DefaultOperation),
            OverwriteExisting = config.OverwriteExisting,
            KeywordRules = config.KeywordRules.Select(CloneRule).ToList(),
            EmbeddingProvider = NormalizeEmbeddingProvider(config.EmbeddingProvider),
            EmbeddingModelPath = NormalizeOptionalText(config.EmbeddingModelPath),
            EmbeddingApiEndpoint = NormalizeOptionalText(config.EmbeddingApiEndpoint),
            EmbeddingApiKey = NormalizeOptionalText(config.EmbeddingApiKey),
            EmbeddingModelName = NormalizeOptionalText(config.EmbeddingModelName),
            EmbeddingSimilarityThreshold = Math.Clamp(config.EmbeddingSimilarityThreshold, 0.0, 1.0),
            LlmEnabled = config.LlmEnabled,
            LlmApiEndpoint = NormalizeOptionalText(config.LlmApiEndpoint),
            LlmApiKey = NormalizeOptionalText(config.LlmApiKey),
            LlmModelName = NormalizeOptionalText(config.LlmModelName),
            LlmFallbackThreshold = Math.Clamp(config.LlmFallbackThreshold, 0.0, 1.0),
            LlmIncludeContent = config.LlmIncludeContent,
            LlmContentMaxChars = Math.Clamp(config.LlmContentMaxChars, 128, 8192),
            ScheduledEnabled = config.ScheduledEnabled,
            ScheduledIntervalMinutes = Math.Max(1, config.ScheduledIntervalMinutes),
        };
    }

    public static string NormalizeOperation(string? operation)
    {
        return (operation ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sort" => "sort",
            "move" => "move",
            "copy" => "copy",
            "delete" => "delete",
            _ => "organize"
        };
    }

    public static string NormalizeEmbeddingProvider(string? provider)
    {
        return (provider ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "local" => "local",
            "remote" => "remote",
            "hybrid" => "hybrid",
            _ => "keyword"
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length == 0 ? null : normalized;
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
            MatchMode = rule.MatchMode,
            Priority = rule.Priority
        };
    }
}
