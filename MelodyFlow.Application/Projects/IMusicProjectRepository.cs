using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Projects;

public interface IMusicProjectRepository
{
    Task AddAsync(MusicProject project, CancellationToken cancellationToken);
    Task<MusicProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record ProjectPage(IReadOnlyList<MusicProject> Items, int Page, int PageSize, int TotalCount);
