using System.Text.Json;
using MelodyFlow.Application.Music;
using MelodyFlow.Application.Media;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
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

    public async Task<IReadOnlyList<MusicTrack>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.MusicTracks.AsNoTracking()
            .Where(track => ids.Contains(track.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<MusicTrack?> GetByGenerationJobIdAsync(Guid generationJobId, CancellationToken cancellationToken) =>
        dbContext.MusicTracks.AsNoTracking().SingleOrDefaultAsync(
            track => track.GenerationJobId == generationJobId,
            cancellationToken);

    public async Task<MusicTrackPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.MusicTracks.CountAsync(cancellationToken);
        var offset = checked((page - 1) * pageSize);
        var items = await dbContext.MusicTracks
            .AsNoTracking()
            .OrderByDescending(track => track.CreatedAt)
            .ThenBy(track => track.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MusicTrackPage(items, page, pageSize, totalCount);
    }

    public async Task<DeleteResourceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var track = await dbContext.MusicTracks.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (track is null)
        {
            return new DeleteResourceResult(DeleteResourceOutcome.NotFound);
        }

        if (await dbContext.RenderedVideoTracks.AnyAsync(item => item.MusicTrackId == id, cancellationToken) ||
            await dbContext.RenderedVideos.AnyAsync(video => video.MusicTrackId == id, cancellationToken) ||
            await HasActiveRenderReferenceAsync(id, cancellationToken))
        {
            return new DeleteResourceResult(DeleteResourceOutcome.InUse);
        }

        dbContext.MusicTracks.Remove(track);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DeleteResourceResult(DeleteResourceOutcome.Deleted, track.StorageKey);
    }

    private async Task<bool> HasActiveRenderReferenceAsync(Guid resourceId, CancellationToken cancellationToken)
    {
        var activeStatuses = new[]
        {
            JobStatus.Pending,
            JobStatus.Queued,
            JobStatus.Running,
            JobStatus.Retrying,
            JobStatus.Cancelling
        };
        var parameters = await dbContext.GenerationJobs.AsNoTracking()
            .Where(job => job.JobType == JobType.Render && activeStatuses.Contains(job.Status))
            .Select(job => job.RequestedParametersJson)
            .ToListAsync(cancellationToken);

        return parameters.Any(json =>
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("musicTrackIds", out var trackIds) &&
                trackIds.ValueKind == JsonValueKind.Array &&
                trackIds.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(value.GetString(), out var parsedId) && parsedId == resourceId))
            {
                return true;
            }

            return root.TryGetProperty("musicTrackId", out var legacyTrackId) &&
                legacyTrackId.ValueKind == JsonValueKind.String &&
                Guid.TryParse(legacyTrackId.GetString(), out var legacyId) &&
                legacyId == resourceId;
        });
    }

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
