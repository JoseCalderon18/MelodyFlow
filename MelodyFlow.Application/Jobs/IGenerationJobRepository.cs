using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Jobs;

/// <summary>Define el acceso a los trabajos persistidos y a su ciclo de vida en la cola PostgreSQL.</summary>
public interface IGenerationJobRepository
{
    /// <summary>Guarda un trabajo nuevo o devuelve el resultado de una petición idempotente anterior.</summary>
    Task<EnqueueGenerationJobResult> EnqueueOrGetAsync(
        GenerationJob job,
        CancellationToken cancellationToken);

    Task<GenerationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CancellationGenerationJobResult> RequestCancellationAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ClaimedGenerationJob?> ClaimNextAsync(
        string workerId,
        IReadOnlyCollection<JobType> supportedJobTypes,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<LeaseRenewalStatus> RenewLeaseAsync(
        Guid jobId,
        Guid leaseToken,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> ReportProgressAsync(
        Guid jobId,
        Guid leaseToken,
        int progressPercent,
        CancellationToken cancellationToken);

    Task<bool> CompleteAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken);

    Task<FailureHandlingResult> RecordFailureAsync(
        Guid jobId,
        Guid leaseToken,
        string errorCode,
        string safeErrorMessage,
        bool isTransient,
        TimeSpan retryDelay,
        CancellationToken cancellationToken);

    Task<bool> MarkCancelledAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken);
}

/// <summary>Indica si una petición creó un trabajo, repitió la misma solicitud o reutilizó una clave con otros datos.</summary>
public enum EnqueueGenerationJobOutcome
{
    Created,
    ExistingRequest,
    IdempotencyConflict
}

/// <summary>Trabajo y resultado de la operación de encolado idempotente.</summary>
public sealed record EnqueueGenerationJobResult(GenerationJob Job, EnqueueGenerationJobOutcome Outcome);

/// <summary>Resultado de solicitar la cancelación de un trabajo.</summary>
public enum CancellationGenerationJobOutcome
{
    NotFound,
    CancellationRequested,
    CancelledImmediately,
    AlreadyCancelling,
    AlreadyCancelled,
    CannotCancelTerminalJob
}

/// <summary>Trabajo asociado a la respuesta de una operación de cancelación.</summary>
public sealed record CancellationGenerationJobResult(
    GenerationJob? Job,
    CancellationGenerationJobOutcome Outcome);

/// <summary>Trabajo reclamado por un Worker junto con su token de propiedad temporal.</summary>
public sealed record ClaimedGenerationJob(GenerationJob Job, Guid LeaseToken);

/// <summary>Resultado de renovar la propiedad de un trabajo en ejecución.</summary>
public enum LeaseRenewalStatus
{
    Renewed,
    CancellationRequested,
    LeaseLost
}

/// <summary>Resultado de registrar un fallo usando la política de retry y el lease actual.</summary>
public enum FailureHandlingResult
{
    Retrying,
    Failed,
    CancellationRequested,
    LeaseLost
}
