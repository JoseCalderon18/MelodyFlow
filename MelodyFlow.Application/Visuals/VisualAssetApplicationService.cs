using MelodyFlow.Application.Music;
using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Visuals;

/// <summary>Valida, almacena y cataloga vídeos importados manualmente.</summary>
public sealed class VisualAssetApplicationService(
    IVisualAssetRepository assets,
    IMusicProjectRepository projects,
    IArtifactStorage storage)
{
    public const long MaxUploadBytes = 512L * 1024 * 1024;

    public async Task<VisualAsset> ImportVideoAsync(
        Guid? musicProjectId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        string? providerName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (musicProjectId == Guid.Empty ||
            musicProjectId is Guid projectId && await projects.GetByIdAsync(projectId, cancellationToken) is null)
        {
            throw new KeyNotFoundException("The selected music project does not exist.");
        }

        if (fileSizeBytes is <= 0 or > MaxUploadBytes)
        {
            throw new InvalidDataException("The video clip must be between 1 byte and 512 MB.");
        }

        var isAcceptedContentType = string.Equals(contentType, "video/mp4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(Path.GetExtension(fileName), ".mp4", StringComparison.OrdinalIgnoreCase) || !isAcceptedContentType)
        {
            throw new InvalidDataException("Only MP4 video clips are accepted.");
        }

        if (!content.CanSeek)
        {
            throw new InvalidDataException("The uploaded video stream cannot be inspected safely.");
        }

        var originalPosition = content.Position;
        var header = new byte[12];
        var headerRead = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = originalPosition;
        if (headerRead < header.Length || !header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            throw new InvalidDataException("The uploaded file is not a valid MP4 container.");
        }

        var assetId = Guid.NewGuid();
        var storageKey = $"video-clips/{assetId:N}/source.mp4";
        var asset = new VisualAsset(
            musicProjectId,
            generationJobId: null,
            VisualAssetType.Video,
            Path.GetFileName(fileName),
            storageKey,
            fileSizeBytes,
            providerName,
            isManualUpload: true,
            id: assetId);

        await storage.SaveAsync(storageKey, content, cancellationToken);
        try
        {
            await assets.AddAsync(asset, cancellationToken);
            return asset;
        }
        catch
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }

    public Task<VisualAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        assets.GetByIdAsync(id, cancellationToken);

    public async Task<VisualAssetPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        return await assets.GetPageAsync(page, pageSize, cancellationToken);
    }

    public Task<Stream?> OpenVideoAsync(VisualAsset asset, CancellationToken cancellationToken) =>
        storage.OpenReadAsync(asset.StorageKey, cancellationToken);

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The requested page is invalid.");
        }
    }
}
