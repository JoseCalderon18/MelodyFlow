using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Domain.Entities;

/// <summary>Archivo visual importado y sus metadatos; nunca guarda bytes del vídeo en PostgreSQL.</summary>
public sealed class VisualAsset
{
    public const int MaxFileNameLength = 255;
    public const int MaxStorageKeyLength = 1_024;
    public const int MaxProviderNameLength = 80;

    private VisualAsset()
    {
    }

    public VisualAsset(
        Guid? musicProjectId,
        Guid? generationJobId,
        VisualAssetType assetType,
        string fileName,
        string storageKey,
        long fileSizeBytes,
        string? providerName,
        bool isManualUpload,
        Guid? id = null)
    {
        if (id == Guid.Empty || musicProjectId == Guid.Empty || generationJobId == Guid.Empty)
        {
            throw new ArgumentException("Optional project and job IDs must be valid GUIDs when supplied.");
        }

        if (!Enum.IsDefined(assetType))
        {
            throw new ArgumentOutOfRangeException(nameof(assetType), assetType, "Unknown visual asset type.");
        }

        if (assetType != VisualAssetType.Video)
        {
            throw new ArgumentException("The current manual import flow accepts video clips only.", nameof(assetType));
        }

        if (fileSizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "Video file size must be positive.");
        }

        Id = id ?? Guid.NewGuid();
        MusicProjectId = musicProjectId;
        GenerationJobId = generationJobId;
        AssetType = assetType;
        FileName = RequiredFileName(fileName);
        StorageKey = ValidateStorageKey(storageKey);
        FileSizeBytes = fileSizeBytes;
        ProviderName = OptionalText(providerName, nameof(providerName), MaxProviderNameLength);
        IsManualUpload = isManualUpload;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid? MusicProjectId { get; private set; }
    public Guid? GenerationJobId { get; private set; }
    public VisualAssetType AssetType { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public long? DurationMs { get; private set; }
    public int? WidthPx { get; private set; }
    public int? HeightPx { get; private set; }
    public string? ProviderName { get; private set; }
    public bool IsManualUpload { get; private set; }
    public string? RightsEvidenceKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void SetProbeMetadata(long durationMs, int widthPx, int heightPx)
    {
        if (durationMs <= 0 || widthPx <= 0 || heightPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMs), "Probed media metadata must be positive.");
        }

        DurationMs = durationMs;
        WidthPx = widthPx;
        HeightPx = heightPx;
    }

    private static string RequiredFileName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = Path.GetFileName(value.Trim());
        if (normalized.Length > MaxFileNameLength || normalized is "." or ".." || normalized.Contains('\0'))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "File name is invalid or too long.");
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

    private static string? OptionalText(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, normalized.Length, $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
