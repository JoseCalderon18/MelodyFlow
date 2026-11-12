using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Visuals;

public interface IVisualAssetRepository
{
    Task<VisualAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<VisualAssetPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task AddAsync(VisualAsset asset, CancellationToken cancellationToken);
}

public sealed record VisualAssetPage(IReadOnlyList<VisualAsset> Items, int Page, int PageSize, int TotalCount);
