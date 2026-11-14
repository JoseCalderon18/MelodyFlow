using System.Text.Json;
using MelodyFlow.Application.Music;
using MelodyFlow.Application.Projects;
using MelodyFlow.Application.Visuals;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using MelodyFlow.Infrastructure.VideoRendering;

namespace MelodyFlow.Worker;

/// <summary>Combina un clip MP4 importado y una secuencia de canciones mediante FFmpeg.</summary>
public sealed class VideoRenderJobHandler(
    IMusicProjectRepository projects,
    IMusicTrackRepository tracks,
    IVisualAssetRepository assets,
    IRenderedVideoRepository videos,
    IArtifactStorage storage,
    LocalVideoRenderer renderer) : IGenerationJobHandler
{
    public JobType JobType => JobType.Render;

    public async Task ExecuteAsync(GenerationJobWorkItem job, CancellationToken cancellationToken)
    {
        var request = ParseRequest(job.RequestedParametersJson);
        var existing = await videos.GetByRenderJobIdAsync(job.Id, cancellationToken);
        if (existing is not null && await storage.ExistsAsync(existing.StorageKey, cancellationToken))
        {
            return;
        }

        var project = await projects.GetByIdAsync(job.MusicProjectId, cancellationToken);
        if (project is null)
        {
            throw new GenerationJobExecutionException(
                "render_project_missing",
                "The project for this video render no longer exists.",
                isTransient: false);
        }

        var loadedTracks = await tracks.GetByIdsAsync(request.MusicTrackIds, cancellationToken);
        var tracksById = loadedTracks.ToDictionary(track => track.Id);
        if (tracksById.Count != request.MusicTrackIds.Count)
        {
            throw new GenerationJobExecutionException(
                "render_music_missing",
                "One or more selected music tracks are missing.",
                isTransient: false);
        }

        var selectedTracks = request.MusicTrackIds.Select(id => tracksById[id]).ToArray();
        if (selectedTracks[0].MusicProjectId != job.MusicProjectId ||
            selectedTracks.Any(track => track.DurationMs is not > 0))
        {
            throw new GenerationJobExecutionException(
                "render_music_missing",
                "The selected music tracks are invalid or have no measured duration.",
                isTransient: false);
        }

        var durationMs = request.TargetDurationSeconds is int targetSeconds
            ? checked(targetSeconds * 1_000L)
            : Math.Min(7_200_000L, selectedTracks.Sum(track => track.DurationMs!.Value));
        if (durationMs is <= 0 or > 7_200_000)
        {
            throw new GenerationJobExecutionException(
                "render_duration_invalid",
                "The requested video duration must be between 1 second and 2 hours.",
                isTransient: false);
        }

        var visual = await assets.GetByIdAsync(request.VisualAssetId, cancellationToken);
        if (visual is null || visual.AssetType != VisualAssetType.Video)
        {
            throw new GenerationJobExecutionException(
                "render_visual_missing",
                "The selected video clip no longer exists.",
                isTransient: false);
        }

        var outputKey = $"videos/{job.MusicProjectId:D}/{job.Id:D}.mp4";
        await job.ReportProgressAsync(5, cancellationToken);
        try
        {
            var artifact = await renderer.RenderAsync(
                visual.StorageKey,
                selectedTracks.Select(track => new VideoMusicSource(track.StorageKey, track.DurationMs!.Value)).ToArray(),
                outputKey,
                durationMs,
                job.ReportProgressAsync,
                cancellationToken);
            await job.ReportProgressAsync(95, cancellationToken);

            var renderedVideo = new RenderedVideo(
                job.MusicProjectId,
                job.Id,
                visual.Id,
                selectedTracks[0].Id,
                request.Title,
                outputKey,
                artifact.DurationMs,
                artifact.WidthPx,
                artifact.HeightPx,
                artifact.Codec,
                artifact.FileSizeBytes,
                request.MusicTrackIds);
            await videos.AddOrGetByRenderJobIdAsync(renderedVideo, cancellationToken);
        }
        catch (VideoRenderingException exception)
        {
            throw new GenerationJobExecutionException(
                exception.ErrorCode,
                exception.SafeMessage,
                exception.IsTransient,
                exception);
        }
    }

    private static RenderRequest ParseRequest(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("visualAssetId", out var assetIdElement) ||
                !assetIdElement.TryGetGuid(out var assetId) || assetId == Guid.Empty ||
                !root.TryGetProperty("title", out var titleElement) ||
                titleElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(titleElement.GetString()) ||
                titleElement.GetString()!.Length > RenderedVideo.MaxTitleLength)
            {
                throw new JsonException("Video render parameters are invalid.");
            }

            var musicTrackIds = new List<Guid>();
            if (root.TryGetProperty("musicTrackIds", out var trackIdsElement) &&
                trackIdsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in trackIdsElement.EnumerateArray())
                {
                    if (!item.TryGetGuid(out var trackId) || trackId == Guid.Empty)
                    {
                        throw new JsonException("Video track IDs are invalid.");
                    }

                    musicTrackIds.Add(trackId);
                }
            }
            else if (root.TryGetProperty("musicTrackId", out var legacyTrackIdElement) &&
                legacyTrackIdElement.TryGetGuid(out var legacyTrackId))
            {
                musicTrackIds.Add(legacyTrackId);
            }

            int? targetDurationSeconds = null;
            if (root.TryGetProperty("targetDurationSeconds", out var durationElement) &&
                durationElement.ValueKind != JsonValueKind.Null)
            {
                if (!durationElement.TryGetInt32(out var targetSeconds) || targetSeconds is < 1 or > 7_200)
                {
                    throw new JsonException("Video duration is invalid.");
                }

                targetDurationSeconds = targetSeconds;
            }

            if (musicTrackIds.Count is < 1 or > RenderedVideo.MaxMusicTrackCount ||
                musicTrackIds.Distinct().Count() != musicTrackIds.Count)
            {
                throw new JsonException("Video track selection is invalid.");
            }

            return new RenderRequest(musicTrackIds, assetId, titleElement.GetString()!.Trim(), targetDurationSeconds);
        }
        catch (JsonException exception)
        {
            throw new GenerationJobExecutionException(
                "render_parameters_invalid",
                "The video render request is invalid. Select music tracks and an MP4 clip again.",
                isTransient: false,
                exception);
        }
    }

    private sealed record RenderRequest(
        IReadOnlyList<Guid> MusicTrackIds,
        Guid VisualAssetId,
        string Title,
        int? TargetDurationSeconds);
}
