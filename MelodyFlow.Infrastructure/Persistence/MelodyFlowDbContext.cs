using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MelodyFlow.Infrastructure.Persistence;

public sealed class MelodyFlowDbContext(DbContextOptions<MelodyFlowDbContext> options) : DbContext(options)
{
    public DbSet<MusicProject> MusicProjects => Set<MusicProject>();
    public DbSet<GenerationJob> GenerationJobs => Set<GenerationJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MelodyFlowDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
