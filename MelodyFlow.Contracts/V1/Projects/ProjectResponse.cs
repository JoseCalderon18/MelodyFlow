namespace MelodyFlow.Contracts.V1.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Title,
    string Genre,
    string? Mood,
    int TargetDurationSeconds,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
