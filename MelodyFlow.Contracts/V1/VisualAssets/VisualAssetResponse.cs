namespace MelodyFlow.Contracts.V1.VisualAssets;

/// <summary>Metadatos y ruta pública para un clip importado, sin exponer su clave local.</summary>
public sealed record VisualAssetResponse(
    Guid Id,
    Guid? MusicProjectId,
    string AssetType,
    string FileName,
    long FileSizeBytes,
    long? DurationMs,
    int? WidthPx,
    int? HeightPx,
    string? ProviderName,
    bool IsManualUpload,
    DateTimeOffset CreatedAt,
    string ContentUrl);
