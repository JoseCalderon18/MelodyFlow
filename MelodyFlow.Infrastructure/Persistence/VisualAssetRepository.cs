using System.Text.Json;
using MelodyFlow.Application.Visuals;
using MelodyFlow.Application.Media;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MelodyFlow.Infrastructure.Persistence;

/// <summary>Persiste los metadatos de clips importados, manteniendo el vídeo en almacenamiento local.</summary>
public sealed class VisualAssetRepository(MelodyFlowDbContext dbContext) : IVisualAssetRepository
{
    public Task<VisualAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.VisualAssets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public async Task<VisualAssetPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.VisualAssets.CountAsync(cancellationToken);
        var offset = checked((page - 1) * pageSize);
        var items = await dbContext.VisualAssets.AsNoTracking()
            .OrderByDescending(asset => asset.CreatedAt)
            .ThenBy(asset => asset.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new VisualAssetPage(items, page, pageSize, totalCount);
    }

    public async Task AddAsync(VisualAsset asset, CancellationToken cancellationToken)
    {
        await dbContext.VisualAssets.AddAsync(asset, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeleteResourceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.VisualAssets.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (asset is null)
        {
            return new DeleteResourceResult(DeleteResourceOutcome.NotFound);
        }

        if (await dbContext.RenderedVideos.AnyAsync(video => video.VisualAssetId == id, cancellationToken) ||
            await HasActiveRenderReferenceAsync(id, cancellationToken))
        {
            return new DeleteResourceResult(DeleteResourceOutcome.InUse);
        }

        dbContext.VisualAssets.Remove(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DeleteResourceResult(DeleteResourceOutcome.Deleted, asset.StorageKey);
    }

    private async Task<bool> HasActiveRenderReferenceAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var activeStatuses = new[]
        {
            JobStatus.Pending,
            JobStatus.Queued,
            JobStatus.Running,
            JobStatus.Retrying,
            JobStatus.Cancelling
        };
        var parameters = await dbContext.GenerationJobs.AsNoTracking()
            .Where(job => job.JobType == JobType.Render && activeStatuses.Contains(job.Status))
            .Select(job => job.RequestedParametersJson)
            .ToListAsync(cancellationToken);

        return parameters.Any(json =>
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("visualAssetId", out var value) &&
                value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(value.GetString(), out var referencedId) &&
                referencedId == assetId;
        });
    }
}
