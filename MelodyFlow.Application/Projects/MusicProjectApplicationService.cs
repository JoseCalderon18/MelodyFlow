using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Projects;

public sealed class MusicProjectApplicationService(IMusicProjectRepository repository)
{
    public async Task<MusicProject> CreateAsync(
        string title,
        string genre,
        string? mood,
        int targetDurationSeconds,
        CancellationToken cancellationToken)
    {
        var project = new MusicProject(title, genre, targetDurationSeconds, mood);
        await repository.AddAsync(project, cancellationToken);
        return project;
    }

    public Task<MusicProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(id, cancellationToken);

    public Task<ProjectPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The requested page is too large.");
        }

        return repository.GetPageAsync(page, pageSize, cancellationToken);
    }
}
