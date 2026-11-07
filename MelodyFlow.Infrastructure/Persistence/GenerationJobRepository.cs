using MelodyFlow.Application.Jobs;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MelodyFlow.Infrastructure.Persistence;

/// <summary>
/// Implementa la cola durable en PostgreSQL, el control de idempotencia y las actualizaciones bajo lease.
/// </summary>
public sealed class GenerationJobRepository(
    MelodyFlowDbContext dbContext,
    GenerationJobRetryPolicy retryPolicy) : IGenerationJobRepository
{
    private const string IdempotencyIndexName = "ux_generation_jobs_project_type_idempotency";
    private const int ExpiredLeaseBatchSize = 20;

    public async Task<EnqueueGenerationJobResult> EnqueueOrGetAsync(
        GenerationJob job,
        CancellationToken cancellationToken)
    {
        // Primero se consulta la clave; el índice único cubre la carrera entre solicitudes simultáneas.
        if (job.IdempotencyKey is not null)
        {
            var existing = await FindIdempotentRequestAsync(job, cancellationToken);
            if (existing is not null)
            {
                return CompareFingerprint(existing, job);
            }
        }

        await dbContext.GenerationJobs.AddAsync(job, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new EnqueueGenerationJobResult(job, EnqueueGenerationJobOutcome.Created);
        }
        catch (DbUpdateException exception) when (IsIdempotencyIndexConflict(exception))
        {
            // Dos peticiones pueden superar la primera consulta a la vez; el índice único elige una ganadora.
            dbContext.Entry(job).State = EntityState.Detached;
            var existing = await FindIdempotentRequestAsync(job, cancellationToken)
                ?? throw new InvalidOperationException("The conflicting idempotent job could not be found.", exception);
            return CompareFingerprint(existing, job);
        }
    }

    public Task<GenerationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.GenerationJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(job => job.Id == id, cancellationToken);

    public async Task<CancellationGenerationJobResult> RequestCancellationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await LoadJobForUpdateAsync(id, cancellationToken);
        if (job is null)
        {
            return new CancellationGenerationJobResult(null, CancellationGenerationJobOutcome.NotFound);
        }

        if (job.Status == JobStatus.Cancelled)
        {
            return new CancellationGenerationJobResult(job, CancellationGenerationJobOutcome.AlreadyCancelled);
        }

        if (job.Status == JobStatus.Cancelling)
        {
            return new CancellationGenerationJobResult(job, CancellationGenerationJobOutcome.AlreadyCancelling);
        }

        if (job.Status is JobStatus.Completed or JobStatus.Failed)
        {
            return new CancellationGenerationJobResult(job, CancellationGenerationJobOutcome.CannotCancelTerminalJob);
        }

        var wasRunning = job.Status == JobStatus.Running;
        job.RequestCancellation();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CancellationGenerationJobResult(
            job,
            wasRunning
                ? CancellationGenerationJobOutcome.CancellationRequested
                : CancellationGenerationJobOutcome.CancelledImmediately);
    }

    public async Task<ClaimedGenerationJob?> ClaimNextAsync(
        string workerId,
        IReadOnlyCollection<JobType> supportedJobTypes,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        // Una transacción recupera leases vencidos y reclama como máximo un trabajo disponible.
        if (supportedJobTypes.Count == 0)
        {
            return null;
        }

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "Lease duration must be positive.");
        }

        var supportedTypeNames = supportedJobTypes.Select(type => type.ToString()).Distinct().ToArray();
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Si un proceso muere deja de renovar el lease; se recuperan sus trabajos bajo bloqueo antes de reclamar otros.
        // SKIP LOCKED permite que varios Workers busquen filas distintas sin esperar entre sí.
        var expiredJobs = await ReadLockedJobsAsync(
            $"""
            SELECT *
            FROM generation_jobs
            WHERE job_type = ANY({supportedTypeNames})
              AND status IN ('Running', 'Cancelling')
              AND (lease_expires_at IS NULL OR lease_expires_at <= {now})
            ORDER BY lease_expires_at NULLS FIRST, created_at
            LIMIT {ExpiredLeaseBatchSize}
            FOR UPDATE SKIP LOCKED
            """,
            cancellationToken);

        foreach (var expiredJob in expiredJobs)
        {
            expiredJob.RecoverExpiredLease(now, retryPolicy.GetDelay(expiredJob.AttemptCount));
        }

        // Solo entran trabajos nuevos o retries cuya fecha programada ya llegó.
        var candidate = (await ReadLockedJobsAsync(
            $"""
            SELECT *
            FROM generation_jobs
            WHERE job_type = ANY({supportedTypeNames})
              AND (
                    status = 'Queued'
                    OR (status = 'Retrying' AND (next_attempt_at IS NULL OR next_attempt_at <= {now}))
                  )
            ORDER BY created_at, id
            LIMIT 1
            FOR UPDATE SKIP LOCKED
            """,
            cancellationToken)).SingleOrDefault();

        if (candidate is null)
        {
            if (expiredJobs.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (candidate.Status == JobStatus.Retrying)
        {
            candidate.Queue();
        }

        candidate.StartAttempt();
        var leaseToken = Guid.NewGuid();
        candidate.AcquireLease(workerId, leaseToken, now.Add(leaseDuration));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ClaimedGenerationJob(candidate, leaseToken);
    }

    public async Task<LeaseRenewalStatus> RenewLeaseAsync(
        Guid jobId,
        Guid leaseToken,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await LoadJobForUpdateAsync(jobId, cancellationToken);
        if (job is null || !job.OwnsLease(leaseToken))
        {
            return LeaseRenewalStatus.LeaseLost;
        }

        if (job.Status == JobStatus.Cancelling)
        {
            await transaction.CommitAsync(cancellationToken);
            return LeaseRenewalStatus.CancellationRequested;
        }

        if (!job.RenewLease(leaseToken, DateTimeOffset.UtcNow.Add(leaseDuration)))
        {
            return LeaseRenewalStatus.LeaseLost;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return LeaseRenewalStatus.Renewed;
    }

    public async Task<bool> ReportProgressAsync(
        Guid jobId,
        Guid leaseToken,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await LoadJobForUpdateAsync(jobId, cancellationToken);
        if (job is null || !job.OwnsLease(leaseToken) || job.Status != JobStatus.Running)
        {
            return false;
        }

        job.ReportProgress(progressPercent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CompleteAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await LoadJobForUpdateAsync(jobId, cancellationToken);
        if (job is null || !job.OwnsLease(leaseToken) || job.Status != JobStatus.Running)
        {
            return false;
        }

        job.MarkCompleted();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<FailureHandlingResult> RecordFailureAsync(
        Guid jobId,
        Guid leaseToken,
        string errorCode,
        string safeErrorMessage,
        bool isTransient,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await LoadJobForUpdateAsync(jobId, cancellationToken);
        if (job is null || !job.OwnsLease(leaseToken))
        {
            return FailureHandlingResult.LeaseLost;
        }

        if (job.Status == JobStatus.Cancelling)
        {
            await transaction.CommitAsync(cancellationToken);
            return FailureHandlingResult.CancellationRequested;
        }

        if (job.Status != JobStatus.Running)
        {
            return FailureHandlingResult.LeaseLost;
        }

        // Solo fallos transitorios con intentos restantes se programan de nuevo.
        var safeCode = Limit(errorCode, 100);
        var safeMessage = Limit(safeErrorMessage, 2_000);
        var canRetry = isTransient && job.AttemptCount < job.MaxAttempts;

        if (canRetry)
        {
            job.MarkRetrying(safeCode, safeMessage, DateTimeOffset.UtcNow.Add(retryDelay));
        }
        else
        {
            job.MarkFailed(safeCode, safeMessage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return canRetry ? FailureHandlingResult.Retrying : FailureHandlingResult.Failed;
    }

    public async Task<bool> MarkCancelledAsync(Guid jobId, Guid leaseToken, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await LoadJobForUpdateAsync(jobId, cancellationToken);
        if (job is null || !job.OwnsLease(leaseToken) || job.Status != JobStatus.Cancelling)
        {
            return false;
        }

        job.MarkCancelled();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<GenerationJob?> FindIdempotentRequestAsync(
        GenerationJob job,
        CancellationToken cancellationToken) =>
        await dbContext.GenerationJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(existing =>
                existing.MusicProjectId == job.MusicProjectId &&
                existing.JobType == job.JobType &&
                existing.IdempotencyKey == job.IdempotencyKey,
                cancellationToken);

    private static EnqueueGenerationJobResult CompareFingerprint(GenerationJob existing, GenerationJob requested) =>
        string.Equals(existing.RequestFingerprint, requested.RequestFingerprint, StringComparison.Ordinal)
            ? new EnqueueGenerationJobResult(existing, EnqueueGenerationJobOutcome.ExistingRequest)
            : new EnqueueGenerationJobResult(existing, EnqueueGenerationJobOutcome.IdempotencyConflict);

    private static bool IsIdempotencyIndexConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdempotencyIndexName
        };

    private async Task<List<GenerationJob>> ReadLockedJobsAsync(
        FormattableString sql,
        CancellationToken cancellationToken)
    {
        var jobs = new List<GenerationJob>();
        await foreach (var job in dbContext.GenerationJobs
            .FromSqlInterpolated(sql)
            .AsTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            jobs.Add(job);
        }

        return jobs;
    }

    private async Task<GenerationJob?> LoadJobForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        var jobs = await ReadLockedJobsAsync(
            $"SELECT * FROM generation_jobs WHERE id = {id} FOR UPDATE",
            cancellationToken);
        return jobs.SingleOrDefault();
    }

    private static string Limit(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
