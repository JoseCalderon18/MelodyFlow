using MelodyFlow.Application.Music;
using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MelodyFlow.Infrastructure.Persistence;

/// <summary>Implementa las consultas de canciones y evita duplicados al recuperar trabajos.</summary>
public sealed class MusicTrackRepository(MelodyFlowDbContext dbContext) : IMusicTrackRepository
{
    private const string GenerationJobIndexName = "ux_music_tracks_generation_job_id";

    // Las lecturas son de solo consulta, por eso EF Core no necesita hacer seguimiento de cambios.
    public Task<MusicTrack?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.MusicTracks.AsNoTracking().SingleOrDefaultAsync(track => track.Id == id, cancellationToken);

    public Task<MusicTrack?> GetByGenerationJobIdAsync(Guid generationJobId, CancellationToken cancellationToken) =>
        dbContext.MusicTracks.AsNoTracking().SingleOrDefaultAsync(
            track => track.GenerationJobId == generationJobId,
            cancellationToken);

    public async Task<MusicTrack> AddOrGetByGenerationJobIdAsync(
        MusicTrack track,
        CancellationToken cancellationToken)
    {
        if (track.GenerationJobId is not Guid generationJobId)
        {
            throw new ArgumentException("A generated track must reference its generation job.", nameof(track));
        }

        var existing = await GetByGenerationJobIdAsync(generationJobId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await dbContext.MusicTracks.AddAsync(track, cancellationToken);
        try
        {
            // El índice único de PostgreSQL también protege frente a dos Workers que compiten.
            await dbContext.SaveChangesAsync(cancellationToken);
            return track;
        }
        catch (DbUpdateException exception) when (IsGenerationJobIndexConflict(exception))
        {
            // Si dos Workers terminan tras una recuperación del lease, el índice único conserva un solo resultado.
            dbContext.Entry(track).State = EntityState.Detached;
            return await GetByGenerationJobIdAsync(generationJobId, cancellationToken)
                ?? throw new InvalidOperationException("The concurrent music track could not be found.", exception);
        }
    }

    private static bool IsGenerationJobIndexConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: GenerationJobIndexName
        };
}
