using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Jobs;

public interface IGenerationJobRepository
{
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

public enum EnqueueGenerationJobOutcome
{
    Created,
    ExistingRequest,
    IdempotencyConflict
}

public sealed record EnqueueGenerationJobResult(GenerationJob Job, EnqueueGenerationJobOutcome Outcome);

public enum CancellationGenerationJobOutcome
{
    NotFound,
    CancellationRequested,
    CancelledImmediately,
    AlreadyCancelling,
    AlreadyCancelled,
    CannotCancelTerminalJob
}

public sealed record CancellationGenerationJobResult(
    GenerationJob? Job,
    CancellationGenerationJobOutcome Outcome);

public sealed record ClaimedGenerationJob(GenerationJob Job, Guid LeaseToken);

public enum LeaseRenewalStatus
{
    Renewed,
    CancellationRequested,
    LeaseLost
}

public enum FailureHandlingResult
{
    Retrying,
    Failed,
    CancellationRequested,
    LeaseLost
}
