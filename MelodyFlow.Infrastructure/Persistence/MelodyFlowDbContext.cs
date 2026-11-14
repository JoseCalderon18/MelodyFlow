using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MelodyFlow.Infrastructure.Persistence;

/// <summary>Punto de entrada de EF Core para proyectos, trabajos y canciones generadas.</summary>
public sealed class MelodyFlowDbContext(DbContextOptions<MelodyFlowDbContext> options) : DbContext(options)
{
    public DbSet<MusicProject> MusicProjects => Set<MusicProject>();
    public DbSet<GenerationJob> GenerationJobs => Set<GenerationJob>();
    public DbSet<MusicTrack> MusicTracks => Set<MusicTrack>();
    public DbSet<VisualAsset> VisualAssets => Set<VisualAsset>();
    public DbSet<RenderedVideo> RenderedVideos => Set<RenderedVideo>();
    public DbSet<RenderedVideoTrack> RenderedVideoTracks => Set<RenderedVideoTrack>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Cada entidad mantiene su configuración de tabla e índices en un archivo separado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MelodyFlowDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
