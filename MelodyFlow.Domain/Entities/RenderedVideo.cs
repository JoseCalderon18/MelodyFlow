namespace MelodyFlow.Domain.Entities;

/// <summary>MP4 ensamblado a partir de una canción y un clip visual reutilizables.</summary>
public sealed class RenderedVideo
{
    public const int MaxTitleLength = 200;
    public const int MaxStorageKeyLength = 1_024;
    public const int MaxCodecLength = 40;
    public const int MaxContainerLength = 16;

    private RenderedVideo()
    {
    }

    public RenderedVideo(
        Guid musicProjectId,
        Guid renderJobId,
        Guid visualAssetId,
        Guid musicTrackId,
        string title,
        string storageKey,
        long durationMs,
        int widthPx,
        int heightPx,
        string codec,
        long fileSizeBytes)
    {
        if (musicProjectId == Guid.Empty || renderJobId == Guid.Empty || visualAssetId == Guid.Empty || musicTrackId == Guid.Empty)
        {
            throw new ArgumentException("Project, job, visual asset and music track IDs are required.");
        }

        Id = Guid.NewGuid();
        MusicProjectId = musicProjectId;
        RenderJobId = renderJobId;
        VisualAssetId = visualAssetId;
        MusicTrackId = musicTrackId;
        Title = RequiredText(title, nameof(title), MaxTitleLength);
        StorageKey = ValidateStorageKey(storageKey);
        DurationMs = Positive(durationMs, nameof(durationMs));
        WidthPx = Positive(widthPx, nameof(widthPx));
        HeightPx = Positive(heightPx, nameof(heightPx));
        Codec = RequiredText(codec, nameof(codec), MaxCodecLength);
        ContainerFormat = "mp4";
        FileSizeBytes = Positive(fileSizeBytes, nameof(fileSizeBytes));
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid MusicProjectId { get; private set; }
    public Guid RenderJobId { get; private set; }
    public Guid VisualAssetId { get; private set; }
    public Guid MusicTrackId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public long DurationMs { get; private set; }
    public int WidthPx { get; private set; }
    public int HeightPx { get; private set; }
    public string Codec { get; private set; } = string.Empty;
    public string ContainerFormat { get; private set; } = "mp4";
    public long FileSizeBytes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static int Positive(int value, string parameterName) =>
        value > 0 ? value : throw new ArgumentOutOfRangeException(parameterName, "Value must be positive.");

    private static long Positive(long value, string parameterName) =>
        value > 0 ? value : throw new ArgumentOutOfRangeException(parameterName, "Value must be positive.");

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, normalized.Length, $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string ValidateStorageKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        var segments = normalized.Split('/');
        if (normalized.Length > MaxStorageKeyLength || normalized.StartsWith('/') || normalized.Contains('\\') ||
            normalized.Contains(':') || segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Storage key must be a safe relative path.", nameof(value));
        }

        return normalized;
    }
}
