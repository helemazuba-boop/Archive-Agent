using System.Text.Json.Serialization;

namespace ArchiveAgent.Models;

public sealed class ArchiveBackendConfig
{
    [JsonPropertyName("default_operation")]
    public string DefaultOperation { get; set; } = "organize";

    [JsonPropertyName("overwrite_existing")]
    public bool OverwriteExisting { get; set; }

    [JsonPropertyName("keyword_rules")]
    public List<KeywordRule> KeywordRules { get; set; } = new();

    [JsonPropertyName("embedding_provider")]
    public string EmbeddingProvider { get; set; } = "keyword";

    [JsonPropertyName("embedding_model_path")]
    public string? EmbeddingModelPath { get; set; }

    [JsonPropertyName("embedding_api_endpoint")]
    public string? EmbeddingApiEndpoint { get; set; }

    [JsonPropertyName("embedding_api_key")]
    public string? EmbeddingApiKey { get; set; }

    [JsonPropertyName("embedding_model_name")]
    public string? EmbeddingModelName { get; set; }

    [JsonPropertyName("embedding_similarity_threshold")]
    public double EmbeddingSimilarityThreshold { get; set; } = 0.7;

    [JsonPropertyName("llm_enabled")]
    public bool LlmEnabled { get; set; } = false;

    [JsonPropertyName("llm_api_endpoint")]
    public string? LlmApiEndpoint { get; set; }

    [JsonPropertyName("llm_api_key")]
    public string? LlmApiKey { get; set; }

    [JsonPropertyName("llm_model_name")]
    public string? LlmModelName { get; set; }

    [JsonPropertyName("llm_fallback_threshold")]
    public double LlmFallbackThreshold { get; set; } = 0.5;

    [JsonPropertyName("llm_include_content")]
    public bool LlmIncludeContent { get; set; } = false;

    [JsonPropertyName("llm_content_max_chars")]
    public int LlmContentMaxChars { get; set; } = 2048;

    [JsonPropertyName("scheduled_enabled")]
    public bool ScheduledEnabled { get; set; } = false;

    [JsonPropertyName("scheduled_interval_minutes")]
    public int ScheduledIntervalMinutes { get; set; } = 60;
}

public sealed class ArchiveBackendConfigPatch
{
    [JsonPropertyName("default_operation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultOperation { get; set; }

    [JsonPropertyName("overwrite_existing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OverwriteExisting { get; set; }

    [JsonPropertyName("keyword_rules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<KeywordRule>? KeywordRules { get; set; }

    [JsonPropertyName("embedding_provider")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingProvider { get; set; }

    [JsonPropertyName("embedding_model_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingModelPath { get; set; }

    [JsonPropertyName("embedding_api_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingApiEndpoint { get; set; }

    [JsonPropertyName("embedding_api_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingApiKey { get; set; }

    [JsonPropertyName("embedding_model_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EmbeddingModelName { get; set; }

    [JsonPropertyName("embedding_similarity_threshold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? EmbeddingSimilarityThreshold { get; set; }

    [JsonPropertyName("llm_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LlmEnabled { get; set; }

    [JsonPropertyName("llm_api_endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LlmApiEndpoint { get; set; }

    [JsonPropertyName("llm_api_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LlmApiKey { get; set; }

    [JsonPropertyName("llm_model_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LlmModelName { get; set; }

    [JsonPropertyName("llm_fallback_threshold")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LlmFallbackThreshold { get; set; }

    [JsonPropertyName("llm_include_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LlmIncludeContent { get; set; }

    [JsonPropertyName("llm_content_max_chars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LlmContentMaxChars { get; set; }

    [JsonPropertyName("scheduled_enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ScheduledEnabled { get; set; }

    [JsonPropertyName("scheduled_interval_minutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ScheduledIntervalMinutes { get; set; }
}

public sealed class ArchiveBackendSnapshot
{
    [JsonPropertyName("config")]
    public ArchiveBackendConfig Config { get; set; } = new();

    [JsonPropertyName("state")]
    public ArchiveState State { get; set; } = new();
}
