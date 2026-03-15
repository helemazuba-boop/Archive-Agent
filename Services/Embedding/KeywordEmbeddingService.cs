using System.Diagnostics;
using ArchiveAgent.Models;

namespace ArchiveAgent.Services.Embedding;

/// <summary>
/// 关键字匹配嵌入服务 - 基于关键字规则匹配的简单实现
/// </summary>
public class KeywordEmbeddingService : IEmbeddingService
{
    private readonly ArchiveConfig _config;
    private readonly Stopwatch _stopwatch = new();

    public EmbeddingConfig Config { get; private set; }
    public EmbeddingProviderType ProviderType => EmbeddingProviderType.Keyword;
    public bool IsAvailable => true;

    public KeywordEmbeddingService(ArchiveConfig config)
    {
        _config = config;
        Config = new EmbeddingConfig
        {
            ProviderType = EmbeddingProviderType.Keyword,
            SimilarityThreshold = 1.0 // 关键字匹配需要完全匹配
        };
    }

    public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        // 关键字匹配不需要真正的嵌入向量，返回空向量
        return Task.FromResult(new EmbeddingResult
        {
            Vector = Array.Empty<float>(),
            ProcessingTimeMs = 0
        });
    }

    public Task<List<EmbeddingResult>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(texts.Select(_ => new EmbeddingResult
        {
            Vector = Array.Empty<float>(),
            ProcessingTimeMs = 0
        }).ToList());
    }

    public double CosineSimilarity(float[] vector1, float[] vector2)
    {
        // 关键字匹配使用简单的字符串匹配
        return vector1.Length == 0 && vector2.Length == 0 ? 1.0 : 0.0;
    }

    public async Task<FileFeatureResult> ExtractFileFeaturesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _stopwatch.Restart();

        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(fileName);
        var result = new FileFeatureResult();

        // 提取关键词
        result.Keywords = ExtractKeywords(fileName);

        // 匹配规则
        var matchedRule = _config.KeywordRules
            .Where(r => r.IsEnabled)
            .OrderByDescending(r => r.Priority)
            .FirstOrDefault(r => MatchRule(r, fileName, extension));

        if (matchedRule != null)
        {
            result.RecommendedDirectory = matchedRule.TargetDirectory;
            result.Confidence = 1.0;
        }

        _stopwatch.Stop();
        result.FileNameEmbedding = new EmbeddingResult
        {
            ProcessingTimeMs = _stopwatch.ElapsedMilliseconds
        };

        return result;
    }

    private List<string> ExtractKeywords(string fileName)
    {
        // 简单的关键词提取：移除扩展名，按常见分隔符分割
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var keywords = new List<string>();

        // 按分隔符分割
        var parts = nameWithoutExt.Split(new[] { ' ', '_', '-', '.', '(', ')', '[', ']' }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        keywords.AddRange(parts.Where(p => p.Length >= 2)); // 过滤太短的词

        return keywords;
    }

    private bool MatchRule(KeywordRule rule, string fileName, string extension)
    {
        if (rule.MatchFileName && fileName.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (rule.MatchExtension && extension.Equals(rule.Keyword, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }
}
