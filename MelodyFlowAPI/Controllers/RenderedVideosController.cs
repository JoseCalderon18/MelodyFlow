using System.ComponentModel.DataAnnotations;
using MelodyFlow.Application.Visuals;
using MelodyFlow.Contracts.V1;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Contracts.V1.RenderedVideos;
using MelodyFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace MelodyFlowAPI.Controllers;

/// <summary>Encola ensamblados MP4, lista resultados y sirve los vídeos generados.</summary>
[ApiController]
[Route("api/v1/rendered-videos")]
public sealed class RenderedVideosController(RenderedVideoApplicationService videos) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerationJobResponse>> Create(
        [FromBody] CreateRenderedVideoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not null &&
            (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > GenerationJob.MaxIdempotencyKeyLength))
        {
            ModelState.AddModelError("Idempotency-Key", "The idempotency key is invalid or too long.");
            return ValidationProblem(ModelState);
        }

        CreateVideoRenderResult result;
        try
        {
            result = await videos.CreateAsync(
                request.MusicProjectId,
                request.MusicTrackId,
                request.VisualAssetId,
                request.Title,
                idempotencyKey,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(request.Title), exception.Message);
            return ValidationProblem(ModelState);
        }

        if (result.Outcome == CreateVideoRenderOutcome.ProjectNotFound)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Music project not found");
        }

        if (result.Outcome == CreateVideoRenderOutcome.TrackNotFound)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Music track not found");
        }

        if (result.Outcome == CreateVideoRenderOutcome.VisualAssetNotFound)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Video clip not found");
        }

        if (result.Outcome == CreateVideoRenderOutcome.IdempotencyConflict)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Idempotency key already used");
        }

        var job = Map(result.Job!);
        return result.Outcome == CreateVideoRenderOutcome.ExistingRequest
            ? Ok(job)
            : AcceptedAtRoute("GetGenerationJobById", new { id = job.Id }, job);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<RenderedVideoResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RenderedVideoResponse>>> GetPage(
        [FromQuery] RenderedVideoPageQuery query,
        CancellationToken cancellationToken)
    {
        var page = await videos.GetPageAsync(query.Page, query.PageSize, cancellationToken);
        return Ok(new PagedResponse<RenderedVideoResponse>(
            page.Items.Select(Map).ToArray(), page.Page, page.PageSize, page.TotalCount));
    }

    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType<RenderedVideoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RenderedVideoResponse>> GetByJob(Guid jobId, CancellationToken cancellationToken)
    {
        var video = await videos.GetByRenderJobIdAsync(jobId, cancellationToken);
        return video is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Rendered video not found")
            : Ok(Map(video));
    }

    [HttpGet("{id:guid}/content")]
    [Produces("video/mp4")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var video = await videos.GetByIdAsync(id, cancellationToken);
        if (video is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Rendered video not found");
        }

        var content = await videos.OpenVideoAsync(video, cancellationToken);
        return content is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Rendered MP4 file not found")
            : File(content, "video/mp4", $"{video.Title}.mp4", enableRangeProcessing: true);
    }

    private static RenderedVideoResponse Map(RenderedVideo video) => new(
        video.Id,
        video.MusicProjectId,
        video.RenderJobId,
        video.VisualAssetId,
        video.MusicTrackId,
        video.Title,
        video.DurationMs,
        video.WidthPx,
        video.HeightPx,
        video.Codec,
        video.ContainerFormat,
        video.FileSizeBytes,
        video.CreatedAt,
        $"/api/v1/rendered-videos/{video.Id:D}/content");

    private static GenerationJobResponse Map(GenerationJob job) => new(
        job.Id,
        job.MusicProjectId,
        job.JobType.ToString(),
        job.Status.ToString(),
        job.ProgressPercent,
        job.AttemptCount,
        job.MaxAttempts,
        job.ErrorCode,
        job.ErrorMessage,
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.UpdatedAt,
        job.NextAttemptAt);
}

public sealed class RenderedVideoPageQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page >= 1 && PageSize >= 1 && (long)(Page - 1) * PageSize > int.MaxValue)
        {
            yield return new ValidationResult("The requested page is too large.", [nameof(Page)]);
        }
    }
}
