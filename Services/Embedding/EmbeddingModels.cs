namespace ArchiveAgent.Services.Embedding;

/// <summary>
/// 文本嵌入向量结果
/// </summary>
public class EmbeddingResult
{
    /// <summary>
    /// 嵌入向量
    /// </summary>
    public float[] Vector { get; set; } = Array.Empty<float>();

    /// <summary>
    /// 向量维度
    /// </summary>
    public int Dimensions => Vector.Length;

    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// 文件特征提取结果
/// </summary>
public class FileFeatureResult
{
    /// <summary>
    /// 文件名嵌入
    /// </summary>
    public EmbeddingResult? FileNameEmbedding { get; set; }

    /// <summary>
    /// 文件内容嵌入（如适用）
    /// </summary>
    public EmbeddingResult? ContentEmbedding { get; set; }

    /// <summary>
    /// 提取的关键词
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// 推荐的目标目录
    /// </summary>
    public string? RecommendedDirectory { get; set; }

    /// <summary>
    /// 推荐置信度 (0-1)
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// 嵌入模型提供者类型
/// </summary>
public enum EmbeddingProviderType
{
    /// <summary>
    /// 关键字匹配（当前实现）
    /// </summary>
    Keyword,

    /// <summary>
    /// 本地模型 (如 sentence-transformers)
    /// </summary>
    LocalModel,

    /// <summary>
    /// 远程 API (如 OpenAI Embeddings)
    /// </summary>
    RemoteApi,

    /// <summary>
    /// 混合模式（关键字 + 嵌入）
    /// </summary>
    Hybrid
}

/// <summary>
/// 嵌入模型配置
/// </summary>
public class EmbeddingConfig
{
    /// <summary>
    /// 提供者类型
    /// </summary>
    public EmbeddingProviderType ProviderType { get; set; } = EmbeddingProviderType.Keyword;

    /// <summary>
    /// 本地模型路径
    /// </summary>
    public string? LocalModelPath { get; set; }

    /// <summary>
    /// 远程 API 端点
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// API Key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 模型名称
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// 相似度阈值 (0-1)
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.7;

    /// <summary>
    /// 是否启用内容提取
    /// </summary>
    public bool EnableContentExtraction { get; set; } = false;

    /// <summary>
    /// 支持的文件扩展名（用于内容提取）
    /// </summary>
    public List<string> SupportedExtensions { get; set; } = new()
    {
        ".txt", ".md", ".json", ".xml", ".csv",
        ".pdf", ".docx", ".xlsx"
    };
}
