using ArchiveAgent.Models;

namespace ArchiveAgent.Services.Embedding;

/// <summary>
/// 嵌入服务工厂 - 根据配置创建合适的嵌入服务
/// </summary>
public class EmbeddingServiceFactory
{
    private readonly ArchiveConfig _config;

    public EmbeddingServiceFactory(ArchiveConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 创建嵌入服务
    /// </summary>
    /// <returns>嵌入服务实例</returns>
    public IEmbeddingService Create()
    {
        return Create(EmbeddingProviderType.Keyword); // 默认使用关键字匹配
    }

    /// <summary>
    /// 创建指定类型的嵌入服务
    /// </summary>
    /// <param name="providerType">提供者类型</param>
    /// <returns>嵌入服务实例</returns>
    public IEmbeddingService Create(EmbeddingProviderType providerType)
    {
        return providerType switch
        {
            EmbeddingProviderType.Keyword => new KeywordEmbeddingService(_config),
            EmbeddingProviderType.LocalModel => new LocalEmbeddingService(new EmbeddingConfig
            {
                ProviderType = EmbeddingProviderType.LocalModel,
                LocalModelPath = _config.EmbeddingModelPath
            }),
            EmbeddingProviderType.RemoteApi => new RemoteEmbeddingService(new EmbeddingConfig
            {
                ProviderType = EmbeddingProviderType.RemoteApi,
                ApiEndpoint = _config.EmbeddingApiEndpoint,
                ApiKey = _config.EmbeddingApiKey,
                ModelName = _config.EmbeddingModelName
            }),
            EmbeddingProviderType.Hybrid => new HybridEmbeddingService(_config),
            _ => new KeywordEmbeddingService(_config)
        };
    }
}

/// <summary>
/// 混合嵌入服务 - 结合关键字匹配和嵌入向量
/// </summary>
public class HybridEmbeddingService : IEmbeddingService
{
    private readonly KeywordEmbeddingService _keywordService;
    private readonly IEmbeddingService? _embeddingService;
    private readonly ArchiveConfig _config;

    public EmbeddingConfig Config { get; private set; }
    public EmbeddingProviderType ProviderType => EmbeddingProviderType.Hybrid;
    public bool IsAvailable => _keywordService.IsAvailable;

    public HybridEmbeddingService(ArchiveConfig config)
    {
        _config = config;
        _keywordService = new KeywordEmbeddingService(config);
        Config = new EmbeddingConfig
        {
            ProviderType = EmbeddingProviderType.Hybrid,
            SimilarityThreshold = 0.7
        };

        // 如果配置了远程 API，创建远程服务
        if (!string.IsNullOrEmpty(config.EmbeddingApiEndpoint) && !string.IsNullOrEmpty(config.EmbeddingApiKey))
        {
            _embeddingService = new RemoteEmbeddingService(new EmbeddingConfig
            {
                ApiEndpoint = config.EmbeddingApiEndpoint,
                ApiKey = config.EmbeddingApiKey,
                ModelName = config.EmbeddingModelName
            });
        }
    }

    public async Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_embeddingService != null && _embeddingService.IsAvailable)
        {
            return await _embeddingService.EmbedAsync(text, cancellationToken);
        }
        return await _keywordService.EmbedAsync(text, cancellationToken);
    }

    public async Task<List<EmbeddingResult>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        if (_embeddingService != null && _embeddingService.IsAvailable)
        {
            return await _embeddingService.EmbedBatchAsync(texts, cancellationToken);
        }
        return await _keywordService.EmbedBatchAsync(texts, cancellationToken);
    }

    public double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length == 0 || vector2.Length == 0)
        {
            return 0.0;
        }

        double dotProduct = 0;
        double magnitude1 = 0;
        double magnitude2 = 0;

        for (int i = 0; i < vector1.Length; i++)
        {
            dotProduct += vector1[i] * vector2[i];
            magnitude1 += vector1[i] * vector1[i];
            magnitude2 += vector2[i] * vector2[i];
        }

        magnitude1 = Math.Sqrt(magnitude1);
        magnitude2 = Math.Sqrt(magnitude2);

        if (magnitude1 == 0 || magnitude2 == 0)
        {
            return 0.0;
        }

        return dotProduct / (magnitude1 * magnitude2);
    }

    public async Task<FileFeatureResult> ExtractFileFeaturesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // 首先尝试关键字匹配
        var keywordResult = await _keywordService.ExtractFileFeaturesAsync(filePath, cancellationToken);
        
        // 如果关键字匹配成功，直接返回
        if (keywordResult.Confidence >= 1.0 && !string.IsNullOrEmpty(keywordResult.RecommendedDirectory))
        {
            return keywordResult;
        }

        // 否则尝试嵌入服务（如果可用）
        if (_embeddingService != null && _embeddingService.IsAvailable)
        {
            try
            {
                var embeddingResult = await _embeddingService.ExtractFileFeaturesAsync(filePath, cancellationToken);
                if (embeddingResult.Confidence >= Config.SimilarityThreshold)
                {
                    return embeddingResult;
                }
            }
            catch
            {
                // 嵌入服务失败，返回关键字结果
            }
        }

        return keywordResult;
    }
}
