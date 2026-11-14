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
        IReadOnlyList<Guid> musicTrackIds,
        Guid visualAssetId,
        int? targetDurationSeconds,
        string? title,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (title?.Trim().Length > RenderedVideo.MaxTitleLength)
        {
            throw new ArgumentException($"Video title cannot exceed {RenderedVideo.MaxTitleLength} characters.", nameof(title));
        }

        if (musicTrackIds.Count is < 1 or > RenderedVideo.MaxMusicTrackCount ||
            musicTrackIds.Any(id => id == Guid.Empty) ||
            musicTrackIds.Distinct().Count() != musicTrackIds.Count)
        {
            throw new ArgumentException("Select between 1 and 20 distinct music tracks.", nameof(musicTrackIds));
        }

        var loadedTracks = await tracks.GetByIdsAsync(musicTrackIds, cancellationToken);
        var tracksById = loadedTracks.ToDictionary(track => track.Id);
        if (tracksById.Count != musicTrackIds.Count)
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.TrackNotFound);
        }

        var orderedTracks = musicTrackIds.Select(id => tracksById[id]).ToArray();
        if (orderedTracks[0].MusicProjectId != projectId)
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.TrackNotInProject);
        }

        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.ProjectNotFound);
        }

        foreach (var track in orderedTracks)
        {
            if (track.DurationMs is not > 0 || !await storage.ExistsAsync(track.StorageKey, cancellationToken))
            {
                return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.TrackNotFound);
            }

            if (track.DurationMs < 15_000)
            {
                throw new ArgumentException("Each song must be at least 15 seconds long to create a video playlist.", nameof(musicTrackIds));
            }
        }

        var resolvedDurationSeconds = targetDurationSeconds ?? (int)Math.Min(
            7_200,
            Math.Ceiling(orderedTracks.Sum(track => track.DurationMs!.Value) / 1_000d));
        if (resolvedDurationSeconds is < 1 or > 7_200)
        {
            throw new ArgumentOutOfRangeException(nameof(targetDurationSeconds), "Video duration must be between 1 second and 2 hours.");
        }

        var visual = await assets.GetByIdAsync(visualAssetId, cancellationToken);
        if (visual is null || !await storage.ExistsAsync(visual.StorageKey, cancellationToken))
        {
            return new CreateVideoRenderResult(null, CreateVideoRenderOutcome.VisualAssetNotFound);
        }

        var resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? RenderedVideoTitleGenerator.Generate(orderedTracks[0].Title, project.Genre, project.Mood)
            : title.Trim();
        var parameters = JsonSerializer.Serialize(new
        {
            musicTrackIds,
            visualAssetId,
            targetDurationSeconds = resolvedDurationSeconds,
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
