namespace MelodyFlow.Contracts.V1.GenerationJobs;

public sealed record GenerationJobResponse(
    Guid Id,
    Guid MusicProjectId,
    string JobType,
    string Status,
    int ProgressPercent,
    int AttemptCount,
    int MaxAttempts,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? NextAttemptAt);
