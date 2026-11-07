using MelodyFlow.Application.Projects;
using MelodyFlow.Application.Jobs;
using MelodyFlow.Application.Music;
using MelodyFlow.Infrastructure.Persistence;
using MelodyFlow.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MelodyFlow.Infrastructure;

/// <summary>
/// Registra adaptadores de persistencia y almacenamiento compartidos por API y Worker.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Configura PostgreSQL, repositorios y almacenamiento multimedia local.</summary>
    /// <param name="connectionString">Conexión a la base MelodyFlow.</param>
    /// <param name="mediaStorageRoot">Carpeta absoluta compartida por API y Worker; si falta, usa LocalAppData.</param>
    public static IServiceCollection AddMelodyFlowInfrastructure(
        this IServiceCollection services,
        string connectionString,
        string? mediaStorageRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Cada proceso registra las mismas implementaciones para consultar la base y los archivos compartidos.
        services.AddDbContext<MelodyFlowDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IMusicProjectRepository, MusicProjectRepository>();
        services.AddScoped<IGenerationJobRepository, GenerationJobRepository>();
        services.AddScoped<IMusicTrackRepository, MusicTrackRepository>();
        services.AddSingleton<IArtifactStorage>(_ => new LocalArtifactStorage(mediaStorageRoot));
        return services;
    }
}
