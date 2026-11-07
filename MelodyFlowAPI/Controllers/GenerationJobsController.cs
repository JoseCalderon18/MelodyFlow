using MelodyFlow.Application.Jobs;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace MelodyFlowAPI.Controllers;

/// <summary>Endpoints para encolar trabajos musicales, consultar su estado y solicitar cancelación.</summary>
[ApiController]
[Route("api/v1/generation-jobs")]
public sealed class GenerationJobsController(GenerationJobApplicationService jobs) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(131_072)]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerationJobResponse>> Create(
        [FromBody] CreateGenerationJobRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        // La misma clave solo puede repetirse con el mismo proyecto, tipo y parámetros normalizados.
        if (idempotencyKey is not null &&
            (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > GenerationJob.MaxIdempotencyKeyLength))
        {
            ModelState.AddModelError(
                "Idempotency-Key",
                $"The idempotency key must contain 1 to {GenerationJob.MaxIdempotencyKeyLength} non-whitespace characters.");
            return ValidationProblem(ModelState);
        }

        // La API solo acepta trabajos realmente soportados por el contrato actual.
        if (!Enum.TryParse<JobType>(request.JobType, ignoreCase: true, out var jobType) || jobType != JobType.Music)
        {
            ModelState.AddModelError(nameof(request.JobType), "Only Music jobs are currently supported.");
            return ValidationProblem(ModelState);
        }

        var requestedParametersJson = request.RequestedParameters?.GetRawText() ?? "{}";
        var result = await jobs.CreateAsync(
            request.MusicProjectId,
            jobType,
            requestedParametersJson,
            idempotencyKey,
            cancellationToken);

        if (result.Outcome == CreateGenerationJobOutcome.ProjectNotFound)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found");
        }

        if (result.Outcome == CreateGenerationJobOutcome.IdempotencyConflict)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency key already used",
                detail: "Use a new Idempotency-Key when the project, job type or parameters change.");
        }

        var response = Map(result.Job!);
        if (result.Outcome == CreateGenerationJobOutcome.ExistingRequest)
        {
            return Ok(response);
        }

        return AcceptedAtRoute("GetGenerationJobById", new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}", Name = "GetGenerationJobById")]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerationJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetByIdAsync(id, cancellationToken);
        return job is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Generation job not found")
            : Ok(Map(job));
    }

    [HttpPost("{id:guid}/cancellation")]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<GenerationJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerationJobResponse>> RequestCancellation(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Un trabajo queued/retrying se cancela al instante; uno activo espera la confirmación del Worker.
        var result = await jobs.RequestCancellationAsync(id, cancellationToken);

        if (result.Outcome == CancellationGenerationJobOutcome.NotFound)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Generation job not found");
        }

        if (result.Outcome == CancellationGenerationJobOutcome.CannotCancelTerminalJob)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Generation job is already finished",
                detail: "Completed or failed jobs cannot be cancelled.");
        }

        var response = Map(result.Job!);
        return result.Outcome is CancellationGenerationJobOutcome.CancellationRequested
            or CancellationGenerationJobOutcome.AlreadyCancelling
                ? AcceptedAtRoute("GetGenerationJobById", new { id }, response)
                : Ok(response);
    }

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
