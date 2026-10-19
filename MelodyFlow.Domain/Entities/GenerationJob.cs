using System.Text.Json;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Domain.Entities;

public sealed class GenerationJob
{
    public const int DefaultMaxAttempts = 3;
    public const int MaxIdempotencyKeyLength = 200;
    public const int MaxLeaseOwnerLength = 120;

    private GenerationJob()
    {
    }

    public GenerationJob(
        Guid musicProjectId,
        JobType jobType,
        string requestedParametersJson,
        string? providerName = null,
        string? idempotencyKey = null,
        int maxAttempts = DefaultMaxAttempts,
        string? requestFingerprint = null)
    {
        if (musicProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project ID is required.", nameof(musicProjectId));
        }

        if (!Enum.IsDefined(jobType))
        {
            throw new ArgumentOutOfRangeException(nameof(jobType), jobType, "Unknown job type.");
        }

        if (maxAttempts is < 1 or > DefaultMaxAttempts)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                maxAttempts,
                $"Max attempts must be between 1 and {DefaultMaxAttempts}.");
        }

        Id = Guid.NewGuid();
        MusicProjectId = musicProjectId;
        JobType = jobType;
        RequestedParametersJson = ValidateJson(requestedParametersJson);
        ProviderName = NormalizeOptional(providerName);
        IdempotencyKey = NormalizeOptional(idempotencyKey);
        RequestFingerprint = NormalizeFingerprint(requestFingerprint, IdempotencyKey);
        MaxAttempts = maxAttempts;
        Status = JobStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid MusicProjectId { get; private set; }
    public JobType JobType { get; private set; }
    public JobStatus Status { get; private set; }
    public string RequestedParametersJson { get; private set; } = "{}";
    public string? ProviderName { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public int ProgressPercent { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? LeaseOwner { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public void Queue()
    {
        EnsureStatus(JobStatus.Pending, JobStatus.Retrying);
        NextAttemptAt = null;
        SetStatus(JobStatus.Queued);
    }

    public void StartAttempt()
    {
        EnsureStatus(JobStatus.Queued);

        if (AttemptCount >= MaxAttempts)
        {
            throw new InvalidOperationException("The job has used all allowed attempts.");
        }

        AttemptCount++;
        StartedAt ??= DateTimeOffset.UtcNow;
        SetStatus(JobStatus.Running);
    }

    public void ReportProgress(int progressPercent)
    {
        EnsureStatus(JobStatus.Running);

        if (progressPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progressPercent),
                progressPercent,
                "Progress must be between 0 and 100.");
        }

        ProgressPercent = progressPercent;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkRetrying(string? errorCode, string? errorMessage)
    {
        MarkRetrying(errorCode, errorMessage, DateTimeOffset.UtcNow);
    }

    public void MarkRetrying(string? errorCode, string? errorMessage, DateTimeOffset nextAttemptAt)
    {
        EnsureStatus(JobStatus.Running);

        if (AttemptCount >= MaxAttempts)
        {
            throw new InvalidOperationException("The job has no attempts remaining for a retry.");
        }

        SetError(errorCode, errorMessage);
        NextAttemptAt = nextAttemptAt;
        ClearLease();
        SetStatus(JobStatus.Retrying);
    }

    public void MarkFailed(string? errorCode, string? errorMessage)
    {
        EnsureStatus(JobStatus.Running, JobStatus.Retrying);
        SetError(errorCode, errorMessage);
        CompletedAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;
        ClearLease();
        SetStatus(JobStatus.Failed);
    }

    public void MarkCompleted()
    {
        EnsureStatus(JobStatus.Running);
        ErrorCode = null;
        ErrorMessage = null;
        ProgressPercent = 100;
        CompletedAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;
        ClearLease();
        SetStatus(JobStatus.Completed);
    }

    public void RequestCancellation()
    {
        switch (Status)
        {
            case JobStatus.Pending:
            case JobStatus.Queued:
            case JobStatus.Retrying:
                CompletedAt = DateTimeOffset.UtcNow;
                NextAttemptAt = null;
                ClearLease();
                SetStatus(JobStatus.Cancelled);
                break;
            case JobStatus.Running:
                SetStatus(JobStatus.Cancelling);
                break;
            case JobStatus.Cancelling:
            case JobStatus.Cancelled:
                // Repetir la petición de cancelación es seguro y no cambia el estado.
                break;
            default:
                throw InvalidTransition(Status, JobStatus.Cancelling);
        }
    }

    public void MarkCancelled()
    {
        EnsureStatus(JobStatus.Cancelling);
        CompletedAt = DateTimeOffset.UtcNow;
        NextAttemptAt = null;
        ClearLease();
        SetStatus(JobStatus.Cancelled);
    }

    public void AcquireLease(string workerId, Guid leaseToken, DateTimeOffset leaseExpiresAt)
    {
        EnsureStatus(JobStatus.Running);

        if (LeaseToken is not null)
        {
            throw new InvalidOperationException("The running job already has an active lease.");
        }

        var normalizedWorkerId = RequiredText(workerId, nameof(workerId), MaxLeaseOwnerLength);

        if (leaseToken == Guid.Empty)
        {
            throw new ArgumentException("A lease token is required.", nameof(leaseToken));
        }

        if (leaseExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseExpiresAt), "Lease expiry must be in the future.");
        }

        LeaseOwner = normalizedWorkerId;
        LeaseToken = leaseToken;
        LeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool RenewLease(Guid leaseToken, DateTimeOffset leaseExpiresAt)
    {
        if (Status != JobStatus.Running || LeaseToken != leaseToken)
        {
            return false;
        }

        if (leaseExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseExpiresAt), "Lease expiry must be in the future.");
        }

        LeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public bool OwnsLease(Guid leaseToken) => LeaseToken == leaseToken;

    public void RecoverExpiredLease(DateTimeOffset now, TimeSpan retryDelay)
    {
        if (LeaseExpiresAt is not null && LeaseExpiresAt > now)
        {
            throw new InvalidOperationException("The job lease has not expired.");
        }

        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay), "Retry delay cannot be negative.");
        }

        if (Status == JobStatus.Cancelling)
        {
            MarkCancelled();
            return;
        }

        EnsureStatus(JobStatus.Running);

        if (AttemptCount >= MaxAttempts)
        {
            MarkFailed("worker_lease_expired", "The worker stopped renewing its job lease.");
            return;
        }

        MarkRetrying(
            "worker_lease_expired",
            "The worker stopped renewing its job lease.",
            now.Add(retryDelay));
    }

    private void SetError(string? errorCode, string? errorMessage)
    {
        ErrorCode = NormalizeOptional(errorCode);
        ErrorMessage = NormalizeOptional(errorMessage);
    }

    private void SetStatus(JobStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(params JobStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw InvalidTransition(Status, allowedStatuses[0]);
        }
    }

    private static InvalidOperationException InvalidTransition(JobStatus current, JobStatus expected) =>
        new($"Cannot transition a job from {current} to the requested state; expected {expected}.");

    private static string ValidateJson(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Requested parameters must contain valid JSON.", nameof(value), exception);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseToken = null;
        LeaseExpiresAt = null;
    }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, normalized.Length, $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeFingerprint(string? fingerprint, string? idempotencyKey)
    {
        if (idempotencyKey is null)
        {
            return null;
        }

        if (idempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(idempotencyKey),
                idempotencyKey.Length,
                $"Idempotency key cannot exceed {MaxIdempotencyKeyLength} characters.");
        }

        if (fingerprint is null || fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("An idempotent request requires a SHA-256 request fingerprint.", nameof(fingerprint));
        }

        return fingerprint.ToLowerInvariant();
    }
}
