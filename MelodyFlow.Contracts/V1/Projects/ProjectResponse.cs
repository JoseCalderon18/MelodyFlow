namespace MelodyFlow.Contracts.V1.Projects;

/// <summary>Representación pública de un proyecto musical en API v1.</summary>
public sealed record ProjectResponse(
    Guid Id,
    string Title,
    string Genre,
    string? Mood,
    int TargetDurationSeconds,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
