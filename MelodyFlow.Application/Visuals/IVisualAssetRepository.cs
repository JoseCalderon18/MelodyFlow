using MelodyFlow.Domain.Entities;
using MelodyFlow.Application.Media;

namespace MelodyFlow.Application.Visuals;

public interface IVisualAssetRepository
{
    Task<VisualAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<VisualAssetPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task AddAsync(VisualAsset asset, CancellationToken cancellationToken);
    /// <summary>Deletes a clip only when no rendered video references it.</summary>
    Task<DeleteResourceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record VisualAssetPage(IReadOnlyList<VisualAsset> Items, int Page, int PageSize, int TotalCount);
