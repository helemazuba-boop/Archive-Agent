using System.Text.Json.Serialization;

namespace ArchiveAgent.Models;

public sealed class ArchiveMatchRequest
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; set; } = string.Empty;
}

public sealed class ArchiveMatchResult
{
    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = [];

    [JsonPropertyName("recommended_directory")]
    public string? RecommendedDirectory { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("matched_rule_id")]
    public string? MatchedRuleId { get; set; }

    [JsonPropertyName("matched_keyword")]
    public string? MatchedKeyword { get; set; }
}

public sealed class ArchiveRunRequest
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "organize";

    [JsonPropertyName("target_path")]
    public string TargetPath { get; set; } = string.Empty;
}

public sealed class ArchiveRunResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("target_path")]
    public string TargetPath { get; set; } = string.Empty;

    [JsonPropertyName("processed_files")]
    public List<ArchiveProcessedFileResult> ProcessedFiles { get; set; } = [];
}

public sealed class ArchiveProcessedFileResult
{
    [JsonPropertyName("source_path")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("target_path")]
    public string? TargetPath { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("matched_keyword")]
    public string? MatchedKeyword { get; set; }
}
