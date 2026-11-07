using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Projects;

/// <summary>Define cómo guardar y consultar proyectos sin acoplar Application a EF Core.</summary>
public interface IMusicProjectRepository
{
    Task AddAsync(MusicProject project, CancellationToken cancellationToken);
    Task<MusicProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProjectPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>Una página de proyectos junto con los datos necesarios para navegar el listado.</summary>
public sealed record ProjectPage(IReadOnlyList<MusicProject> Items, int Page, int PageSize, int TotalCount);
