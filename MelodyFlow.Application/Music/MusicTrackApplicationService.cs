using MelodyFlow.Application.Jobs;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Music;

/// <summary>
/// Resuelve consultas de canciones generadas combinando el estado del trabajo y sus metadatos.
/// </summary>
public sealed class MusicTrackApplicationService(
    IMusicTrackRepository tracks,
    IGenerationJobRepository jobs)
{
    /// <summary>Busca una canción por su identificador sin exponer detalles de persistencia.</summary>
    public Task<MusicTrack?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        tracks.GetByIdAsync(id, cancellationToken);

    /// <summary>Distingue entre trabajo inexistente, audio aún no listo y resultado disponible.</summary>
    public async Task<GenerationJobTrackResult> GetByGenerationJobIdAsync(
        Guid generationJobId,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(generationJobId, cancellationToken);
        if (job is null)
        {
            return new GenerationJobTrackResult(null, GenerationJobTrackOutcome.JobNotFound);
        }

        var track = await tracks.GetByGenerationJobIdAsync(generationJobId, cancellationToken);
        if (track is not null)
        {
            return new GenerationJobTrackResult(track, GenerationJobTrackOutcome.Available);
        }

        return job.Status == JobStatus.Completed
            ? new GenerationJobTrackResult(null, GenerationJobTrackOutcome.CompletedWithoutTrack)
            : new GenerationJobTrackResult(null, GenerationJobTrackOutcome.NotReady);
    }
}

/// <summary>Resultado posible al consultar el audio asociado a un trabajo.</summary>
public enum GenerationJobTrackOutcome
{
    JobNotFound,
    NotReady,
    CompletedWithoutTrack,
    Available
}

/// <summary>Incluye la canción cuando está disponible y el motivo cuando no lo está.</summary>
public sealed record GenerationJobTrackResult(MusicTrack? Track, GenerationJobTrackOutcome Outcome);
