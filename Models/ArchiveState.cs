using System.Text.Json.Serialization;

namespace ArchiveAgent.Models;

public sealed class ArchiveState
{
    [JsonPropertyName("operation_history")]
    public List<ArchiveHistoryRecord> OperationHistory { get; set; } = new();

    [JsonPropertyName("last_processed_at")]
    public DateTimeOffset? LastProcessedAt { get; set; }

    [JsonPropertyName("last_error_message")]
    public string? LastErrorMessage { get; set; }
}

public sealed class ArchiveHistoryRecord
{
    [JsonPropertyName("occurred_at")]
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.Now;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

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
