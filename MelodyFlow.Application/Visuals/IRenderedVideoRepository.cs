using MelodyFlow.Domain.Entities;
using MelodyFlow.Application.Media;

namespace MelodyFlow.Application.Visuals;

public interface IRenderedVideoRepository
{
    Task<RenderedVideo?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<RenderedVideo?> GetByRenderJobIdAsync(Guid renderJobId, CancellationToken cancellationToken);
    Task<RenderedVideoPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<DeleteResourceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<RenderedVideo> AddOrGetByRenderJobIdAsync(RenderedVideo video, CancellationToken cancellationToken);
}

public sealed record RenderedVideoPage(IReadOnlyList<RenderedVideo> Items, int Page, int PageSize, int TotalCount);
