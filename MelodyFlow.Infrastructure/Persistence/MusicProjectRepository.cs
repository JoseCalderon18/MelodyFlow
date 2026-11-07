using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MelodyFlow.Infrastructure.Persistence;

/// <summary>Persiste proyectos y devuelve listados estables con paginación.</summary>
public sealed class MusicProjectRepository(MelodyFlowDbContext dbContext) : IMusicProjectRepository
{
    public async Task AddAsync(MusicProject project, CancellationToken cancellationToken)
    {
        await dbContext.MusicProjects.AddAsync(project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<MusicProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.MusicProjects.SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public async Task<ProjectPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        // Se ordena también por Id para que proyectos con la misma fecha no cambien de página al azar.
        var totalCount = await dbContext.MusicProjects.CountAsync(cancellationToken);
        var offset = checked((page - 1) * pageSize);
        var items = await dbContext.MusicProjects
            .AsNoTracking()
            .OrderByDescending(project => project.CreatedAt)
            .ThenBy(project => project.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ProjectPage(items, page, pageSize, totalCount);
    }
}
