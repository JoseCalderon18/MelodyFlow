using MelodyFlow.Application.Music;
using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Worker;

/// <summary>
/// Ejecuta el flujo musical completo y guarda los metadatos que consultará la API.
/// </summary>
public sealed class MusicGenerationJobHandler(
    IMusicProjectRepository projects,
    IMusicTrackRepository tracks,
    IArtifactStorage artifactStorage,
    IMusicGenerationPipeline pipeline) : IGenerationJobHandler
{
    public JobType JobType => JobType.Music;

    public async Task ExecuteAsync(GenerationJobWorkItem job, CancellationToken cancellationToken)
    {
        // El proyecto aporta título, estilo, ambiente y duración a la composición.
        var project = await projects.GetByIdAsync(job.MusicProjectId, cancellationToken);
        if (project is null)
        {
            throw new GenerationJobExecutionException(
                "music_project_missing",
                "The project for this generation job no longer exists.",
                isTransient: false);
        }

        // El Worker puede reiniciarse tras guardar los metadatos y antes de marcar el trabajo como completado.
        // Recuperar una ejecución ya terminada no debe crear otra canción para el mismo trabajo.
        var existingTrack = await tracks.GetByGenerationJobIdAsync(job.Id, cancellationToken);
        if (existingTrack is not null &&
            await artifactStorage.ExistsAsync(existingTrack.StorageKey, cancellationToken))
        {
            return;
        }

        try
        {
            var track = await pipeline.GenerateAsync(
                job.Id,
                project,
                job.RequestedParametersJson,
                job.ReportProgressAsync,
                cancellationToken);
            await tracks.AddOrGetByGenerationJobIdAsync(track, cancellationToken);
        }
        catch (MusicGenerationException exception)
        {
            throw new GenerationJobExecutionException(
                exception.ErrorCode,
                exception.SafeMessage,
                exception.IsTransient,
                exception);
        }
    }
}
