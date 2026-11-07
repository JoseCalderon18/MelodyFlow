using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Tests.Domain;

/// <summary>Comprueba invariantes de estados, reintentos, cancelación y leases de GenerationJob.</summary>
public sealed class GenerationJobTests
{
    [Fact]
    public void RetryLifecycleCountsThreeTotalAttempts()
    {
        var job = CreateJob();

        job.Queue();
        job.StartAttempt();
        job.MarkRetrying("temporary", "Try again");
        job.Queue();
        job.StartAttempt();
        job.MarkRetrying("temporary", "Try again");
        job.Queue();
        job.StartAttempt();
        job.MarkCompleted();

        Assert.Equal(3, job.AttemptCount);
        Assert.Equal(100, job.ProgressPercent);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void JobCannotRetryAfterItsFinalAttempt()
    {
        var job = CreateJob(maxAttempts: 1);
        job.Queue();
        job.StartAttempt();

        Assert.Throws<InvalidOperationException>(() => job.MarkRetrying("temporary", "Try again"));
    }

    [Fact]
    public void RunningCancellationIsPersistedUntilWorkerStops()
    {
        var job = CreateJob();
        job.Queue();
        job.StartAttempt();

        job.RequestCancellation();

        Assert.Equal(JobStatus.Cancelling, job.Status);
        Assert.Null(job.CompletedAt);

        job.MarkCancelled();

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void RetryingJobCanBeCancelledImmediately()
    {
        var job = CreateJob();
        job.Queue();
        job.StartAttempt();
        job.MarkRetrying("temporary", "Try again");

        job.RequestCancellation();

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void RequestedParametersMustBeValidJson()
    {
        Assert.Throws<ArgumentException>(() => new GenerationJob(Guid.NewGuid(), JobType.Music, "not json"));
    }

    [Fact]
    public void LeaseCanOnlyBeRenewedByItsCurrentOwnerAndExpiredAttemptIsScheduledForRetry()
    {
        var job = CreateJob();
        job.Queue();
        job.StartAttempt();
        var leaseToken = Guid.NewGuid();
        job.AcquireLease("worker-test", leaseToken, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.False(job.RenewLease(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2)));
        Assert.True(job.RenewLease(leaseToken, DateTimeOffset.UtcNow.AddMinutes(2)));

        var recoveredAt = DateTimeOffset.UtcNow.AddMinutes(3);
        job.RecoverExpiredLease(recoveredAt, TimeSpan.FromSeconds(10));

        Assert.Equal(JobStatus.Retrying, job.Status);
        Assert.Equal(recoveredAt.AddSeconds(10), job.NextAttemptAt);
        Assert.Null(job.LeaseToken);
        Assert.Equal("worker_lease_expired", job.ErrorCode);
    }

    [Fact]
    public void ExpiredLeaseAfterLastAttemptFailsInsteadOfStartingAnotherAttempt()
    {
        var job = CreateJob(maxAttempts: 1);
        job.Queue();
        job.StartAttempt();
        job.AcquireLease("worker-test", Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(1));

        job.RecoverExpiredLease(DateTimeOffset.UtcNow.AddSeconds(2), TimeSpan.FromSeconds(5));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.LeaseToken);
    }

    private static GenerationJob CreateJob(int maxAttempts = GenerationJob.DefaultMaxAttempts) =>
        new(Guid.NewGuid(), JobType.Music, "{\"tempo\":80}", maxAttempts: maxAttempts);
}
