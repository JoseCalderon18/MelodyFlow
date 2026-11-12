using System.Text.Json;
using MelodyFlow.Application.Music;
using MelodyFlow.Application.Projects;
using MelodyFlow.Application.Visuals;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using MelodyFlow.Infrastructure.VideoRendering;

namespace MelodyFlow.Worker;

/// <summary>Combina un clip MP4 importado y una pista MP3 mediante FFmpeg.</summary>
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

        var track = await tracks.GetByIdAsync(request.MusicTrackId, cancellationToken);
        if (track is null || track.DurationMs is not long durationMs || durationMs <= 0 || durationMs > 1_800_000)
        {
            throw new GenerationJobExecutionException(
                "render_music_missing",
                "The selected music track is missing or has an unsupported duration.",
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
                track.StorageKey,
                outputKey,
                durationMs,
                job.ReportProgressAsync,
                cancellationToken);
            await job.ReportProgressAsync(95, cancellationToken);

            var renderedVideo = new RenderedVideo(
                job.MusicProjectId,
                job.Id,
                visual.Id,
                track.Id,
                request.Title,
                outputKey,
                artifact.DurationMs,
                artifact.WidthPx,
                artifact.HeightPx,
                artifact.Codec,
                artifact.FileSizeBytes);
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
                !root.TryGetProperty("musicTrackId", out var trackIdElement) ||
                !trackIdElement.TryGetGuid(out var trackId) || trackId == Guid.Empty ||
                !root.TryGetProperty("visualAssetId", out var assetIdElement) ||
                !assetIdElement.TryGetGuid(out var assetId) || assetId == Guid.Empty ||
                !root.TryGetProperty("title", out var titleElement) ||
                titleElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(titleElement.GetString()) ||
                titleElement.GetString()!.Length > RenderedVideo.MaxTitleLength)
            {
                throw new JsonException("Video render parameters are invalid.");
            }

            return new RenderRequest(trackId, assetId, titleElement.GetString()!.Trim());
        }
        catch (JsonException exception)
        {
            throw new GenerationJobExecutionException(
                "render_parameters_invalid",
                "The video render request is invalid. Select a music track and an MP4 clip again.",
                isTransient: false,
                exception);
        }
    }

    private sealed record RenderRequest(Guid MusicTrackId, Guid VisualAssetId, string Title);
}
