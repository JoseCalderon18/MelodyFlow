using MelodyFlow.Application.Visuals;
using MelodyFlow.Application.Media;
using MelodyFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MelodyFlow.Infrastructure.Persistence;

/// <summary>Persiste resultados de FFmpeg y evita duplicados al recuperar un render con lease vencido.</summary>
public sealed class RenderedVideoRepository(MelodyFlowDbContext dbContext) : IRenderedVideoRepository
{
    private const string RenderJobIndexName = "ux_rendered_videos_render_job_id";

    public Task<RenderedVideo?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.RenderedVideos.AsNoTracking()
            .Include(video => video.MusicTracks.OrderBy(track => track.SequenceNumber))
            .SingleOrDefaultAsync(video => video.Id == id, cancellationToken);

    public Task<RenderedVideo?> GetByRenderJobIdAsync(Guid renderJobId, CancellationToken cancellationToken) =>
        dbContext.RenderedVideos.AsNoTracking()
            .Include(video => video.MusicTracks.OrderBy(track => track.SequenceNumber))
            .SingleOrDefaultAsync(video => video.RenderJobId == renderJobId, cancellationToken);

    public async Task<RenderedVideoPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var totalCount = await dbContext.RenderedVideos.CountAsync(cancellationToken);
        var offset = checked((page - 1) * pageSize);
        var items = await dbContext.RenderedVideos.AsNoTracking()
            .Include(video => video.MusicTracks.OrderBy(track => track.SequenceNumber))
            .OrderByDescending(video => video.CreatedAt)
            .ThenBy(video => video.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new RenderedVideoPage(items, page, pageSize, totalCount);
    }

    public async Task<DeleteResourceResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var video = await dbContext.RenderedVideos.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (video is null)
        {
            return new DeleteResourceResult(DeleteResourceOutcome.NotFound);
        }

        dbContext.RenderedVideos.Remove(video);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DeleteResourceResult(DeleteResourceOutcome.Deleted, video.StorageKey);
    }

    public async Task<RenderedVideo> AddOrGetByRenderJobIdAsync(RenderedVideo video, CancellationToken cancellationToken)
    {
        var existing = await GetByRenderJobIdAsync(video.RenderJobId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await dbContext.RenderedVideos.AddAsync(video, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return video;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: RenderJobIndexName })
        {
            dbContext.Entry(video).State = EntityState.Detached;
            return await GetByRenderJobIdAsync(video.RenderJobId, cancellationToken)
                ?? throw new InvalidOperationException("The concurrent rendered video could not be found.", exception);
        }
    }
}
