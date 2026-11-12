using MelodyFlow.Application.Visuals;
using MelodyFlow.Domain.Entities;
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
}
