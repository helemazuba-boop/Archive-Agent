namespace ArchiveAgent.Services.Embedding;

/// <summary>
/// 嵌入模型服务接口 - 为未来接入 Text Embeddings 和本地模型预留
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// 当前配置
    /// </summary>
    EmbeddingConfig Config { get; }

    /// <summary>
    /// 提供者类型
    /// </summary>
    EmbeddingProviderType ProviderType { get; }

    /// <summary>
    /// 是否可用
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 计算文本嵌入向量
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入结果</returns>
    Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量计算嵌入向量
    /// </summary>
    /// <param name="texts">文本列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入结果列表</returns>
    Task<List<EmbeddingResult>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算两个向量的余弦相似度
    /// </summary>
    /// <param name="vector1">向量1</param>
    /// <param name="vector2">向量2</param>
    /// <returns>相似度 (0-1)</returns>
    double CosineSimilarity(float[] vector1, float[] vector2);

    /// <summary>
    /// 从文件提取特征
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件特征结果</returns>
    Task<FileFeatureResult> ExtractFileFeaturesAsync(string filePath, CancellationToken cancellationToken = default);
}
