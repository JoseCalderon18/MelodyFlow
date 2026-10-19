using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Domain.Entities;

public sealed class MusicProject
{
    private MusicProject()
    {
    }

    public MusicProject(string title, string genre, int targetDurationSeconds, string? mood = null)
    {
        Id = Guid.NewGuid();
        Status = ProjectStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        SetDetails(title, genre, mood, targetDurationSeconds);
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Genre { get; private set; } = string.Empty;
    public string? Mood { get; private set; }
    public int TargetDurationSeconds { get; private set; }
    public ProjectStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void UpdateDetails(string title, string genre, string? mood, int targetDurationSeconds)
    {
        SetDetails(title, genre, mood, targetDurationSeconds);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeStatus(ProjectStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown project status.");
        }

        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void SetDetails(string title, string genre, string? mood, int targetDurationSeconds)
    {
        Title = RequiredText(title, nameof(title), 200);
        Genre = RequiredText(genre, nameof(genre), 80);
        Mood = OptionalText(mood, nameof(mood), 80);

        if (targetDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetDurationSeconds),
                targetDurationSeconds,
                "Target duration must be greater than zero.");
        }

        TargetDurationSeconds = targetDurationSeconds;
    }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                normalized.Length,
                $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? OptionalText(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                normalized.Length,
                $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
