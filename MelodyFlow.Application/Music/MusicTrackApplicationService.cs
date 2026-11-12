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

    /// <summary>Valida la paginación y consulta la biblioteca sin exponer persistencia.</summary>
    public Task<MusicTrackPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
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

        return tracks.GetPageAsync(page, pageSize, cancellationToken);
    }

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
