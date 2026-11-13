using System.Text.Json;
using MelodyFlow.Application.Jobs;
using MelodyFlow.Application.Media;
using MelodyFlow.Application.Music;
using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Visuals;

/// <summary>Valida las entradas y encola el ensamblado del videoclip en el Worker.</summary>
public sealed class RenderedVideoApplicationService(
    IRenderedVideoRepository videos,
    IVisualAssetRepository assets,
    IMusicTrackRepository tracks,
    IMusicProjectRepository projects,
    IArtifactStorage storage,
    GenerationJobApplicationService jobs)
{
    public async Task<CreateVideoRenderResult> CreateAsync(
        Guid projectId,
        Guid musicTrackId,
        Guid visualAssetId,
        string? title,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (title?.Trim().Length > RenderedVideo.MaxTitleLength)
        {
            throw new ArgumentException($"Video title cannot exceed {RenderedVideo.MaxTitleLength} characters.", nameof(title));
        }

        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.ProjectNotFound);
        }

        var track = await tracks.GetByIdAsync(musicTrackId, cancellationToken);
        if (track is null || !await storage.ExistsAsync(track.StorageKey, cancellationToken))
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.TrackNotFound);
        }

        if (track.MusicProjectId != project.Id)
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.TrackNotInProject);
        }

        var visual = await assets.GetByIdAsync(visualAssetId, cancellationToken);
        if (visual is null || !await storage.ExistsAsync(visual.StorageKey, cancellationToken))
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.VisualAssetNotFound);
        }

        if (track.DurationMs is not > 0 || track.DurationMs > 1_800_000)
        {
            throw new ArgumentException("Video rendering supports music tracks up to 30 minutes with a measured duration.");
        }

        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? RenderedVideoTitleGenerator.Generate(track.Title, project.Genre, project.Mood)
            : title.Trim();
        var parameters = JsonSerializer.Serialize(new
        {
            musicTrackId,
            visualAssetId,
            title = resolvedTitle
        });
        var result = await jobs.CreateAsync(projectId, JobType.Render, parameters, idempotencyKey, cancellationToken);
        return result.Outcome switch
        {
            CreateGenerationJobOutcome.ProjectNotFound => new CreateVideoRenderResult(null, CreateVideoRenderOutcome.ProjectNotFound),
            CreateGenerationJobOutcome.IdempotencyConflict => new CreateVideoRenderResult(result.Job, CreateVideoRenderOutcome.IdempotencyConflict),
            CreateGenerationJobOutcome.ExistingRequest => new CreateVideoRenderResult(result.Job, CreateVideoRenderOutcome.ExistingRequest),
            _ => new CreateVideoRenderResult(result.Job, CreateVideoRenderOutcome.Created)
        };
    }

    public Task<RenderedVideo?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        videos.GetByIdAsync(id, cancellationToken);

    public Task<RenderedVideo?> GetByRenderJobIdAsync(Guid jobId, CancellationToken cancellationToken) =>
        videos.GetByRenderJobIdAsync(jobId, cancellationToken);

    public async Task<RenderedVideoPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The requested page is invalid.");
        }

        return await videos.GetPageAsync(page, pageSize, cancellationToken);
    }

    public Task<Stream?> OpenVideoAsync(RenderedVideo video, CancellationToken cancellationToken) =>
        storage.OpenReadAsync(video.StorageKey, cancellationToken);

    public async Task<DeleteResourceOutcome> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await videos.DeleteAsync(id, cancellationToken);
        if (result.Outcome != DeleteResourceOutcome.Deleted || result.StorageKey is null)
        {
            return result.Outcome;
        }

        try
        {
            await storage.DeleteAsync(result.StorageKey, CancellationToken.None);
            return DeleteResourceOutcome.Deleted;
        }
        catch (IOException)
        {
            return DeleteResourceOutcome.FileCleanupFailed;
        }
        catch (UnauthorizedAccessException)
        {
            return DeleteResourceOutcome.FileCleanupFailed;
        }
    }
}

public enum CreateVideoRenderOutcome
{
    Created,
    ExistingRequest,
    IdempotencyConflict,
    ProjectNotFound,
    TrackNotFound,
    VisualAssetNotFound,
    TrackNotInProject
}

public sealed record CreateVideoRenderResult(GenerationJob? Job, CreateVideoRenderOutcome Outcome);
