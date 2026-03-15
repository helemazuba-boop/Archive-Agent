namespace ArchiveAgent.Services.Embedding;

/// <summary>
/// 远程 API 嵌入服务 - 预留实现，未来支持 OpenAI 等远程 API
/// </summary>
public class RemoteEmbeddingService : IEmbeddingService
{
    private readonly HttpClient? _httpClient;

    public EmbeddingConfig Config { get; private set; }
    public EmbeddingProviderType ProviderType => EmbeddingProviderType.RemoteApi;
    public bool IsAvailable => !string.IsNullOrEmpty(Config.ApiEndpoint) && !string.IsNullOrEmpty(Config.ApiKey);

    public RemoteEmbeddingService(EmbeddingConfig config, HttpClient? httpClient = null)
    {
        Config = config;
        _httpClient = httpClient;
    }

    public async Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("远程嵌入服务未配置 API 端点或 API Key。");
        }

        // TODO: 实现远程 API 调用
        // 示例结构：
        // var request = new { model = Config.ModelName, input = text };
        // var response = await _httpClient.PostAsJsonAsync(Config.ApiEndpoint, request, cancellationToken);
        // 解析响应并返回嵌入向量

        throw new NotImplementedException("远程嵌入 API 服务尚未实现。");
    }

    public async Task<List<EmbeddingResult>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("远程嵌入服务未配置 API 端点或 API Key。");
        }

        // TODO: 实现批量嵌入
        throw new NotImplementedException("远程嵌入 API 服务尚未实现。");
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

    public async Task<FileFeatureResult> ExtractFileFeaturesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("远程嵌入服务未配置 API 端点或 API Key。");
        }

        // TODO: 提取文件特征
        throw new NotImplementedException("远程嵌入 API 服务尚未实现。");
    }
}
