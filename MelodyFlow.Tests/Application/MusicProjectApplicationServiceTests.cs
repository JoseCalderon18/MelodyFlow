using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Tests.Application;

/// <summary>Comprueba la orquestación de proyectos y la validación de paginación de Application.</summary>
public sealed class MusicProjectApplicationServiceTests
{
    [Fact]
    public async Task CreatePersistsProjectThroughRepositoryPort()
    {
        var repository = new InMemoryProjectRepository();
        var service = new MusicProjectApplicationService(repository);

        var project = await service.CreateAsync("Night Drive", "Lo-fi", null, 3600, CancellationToken.None);

        Assert.Same(project, repository.SavedProject);
    }

    [Fact]
    public async Task PageOffsetBeyondIntRangeIsRejectedBeforeRepositoryCall()
    {
        var repository = new InMemoryProjectRepository();
        var service = new MusicProjectApplicationService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetPageAsync(int.MaxValue, 100, CancellationToken.None));
        Assert.False(repository.PageRequested);
    }

    private sealed class InMemoryProjectRepository : IMusicProjectRepository
    {
        public MusicProject? SavedProject { get; private set; }
        public bool PageRequested { get; private set; }

        public Task AddAsync(MusicProject project, CancellationToken cancellationToken)
        {
            SavedProject = project;
            return Task.CompletedTask;
        }

        public Task<MusicProject?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(SavedProject?.Id == id ? SavedProject : null);

        public Task<ProjectPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
        {
            PageRequested = true;
            return Task.FromResult(new ProjectPage([], page, pageSize, 0));
        }
    }
}
