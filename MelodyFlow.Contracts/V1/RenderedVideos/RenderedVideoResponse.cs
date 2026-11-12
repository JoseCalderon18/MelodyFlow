namespace MelodyFlow.Contracts.V1.RenderedVideos;

/// <summary>Metadatos de un MP4 finalizado y ruta de reproducción/descarga.</summary>
public sealed record RenderedVideoResponse(
    Guid Id,
    Guid MusicProjectId,
    Guid RenderJobId,
    Guid VisualAssetId,
    Guid MusicTrackId,
    string Title,
    long DurationMs,
    int WidthPx,
    int HeightPx,
    string Codec,
    string ContainerFormat,
    long FileSizeBytes,
    DateTimeOffset CreatedAt,
    string VideoUrl);
