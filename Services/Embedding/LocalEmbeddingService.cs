namespace ArchiveAgent.Services.Embedding;

/// <summary>
/// 本地嵌入模型服务 - 预留实现，未来支持本地模型
/// </summary>
public class LocalEmbeddingService : IEmbeddingService
{
    public EmbeddingConfig Config { get; private set; }
    public EmbeddingProviderType ProviderType => EmbeddingProviderType.LocalModel;
    public bool IsAvailable => false; // 尚未实现

    public LocalEmbeddingService(EmbeddingConfig config)
    {
        Config = config;
    }

    public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("本地嵌入模型服务尚未实现。请使用 KeywordEmbeddingService 或配置远程 API。");
    }

    public Task<List<EmbeddingResult>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("本地嵌入模型服务尚未实现。请使用 KeywordEmbeddingService 或配置远程 API。");
    }

    public double CosineSimilarity(float[] vector1, float[] vector2)
    {
        if (vector1.Length != vector2.Length || vector1.Length == 0)
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

    public Task<FileFeatureResult> ExtractFileFeaturesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("本地嵌入模型服务尚未实现。");
    }
}
