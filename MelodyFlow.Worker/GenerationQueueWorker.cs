using MelodyFlow.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Worker;

public sealed class GenerationQueueWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<GenerationQueueOptions> options,
    GenerationJobRetryPolicy retryPolicy,
    ILogger<GenerationQueueWorker> logger) : BackgroundService
{
    private readonly GenerationQueueOptions _options = Validate(options.Value);
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    private bool _reportedMissingHandlers;
    private bool _reportedDuplicateHandlers;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Generation queue worker started as {WorkerId}.", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOneIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Queue polling failed ({ExceptionType}); polling will resume after the configured delay.",
                    exception.GetType().Name);
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
        }

        logger.LogInformation("Generation queue worker stopped.");
    }

    private async Task RunOneIterationAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IGenerationJobHandler>().ToArray();

        if (handlers.Length == 0)
        {
            if (!_reportedMissingHandlers)
            {
                // Se conserva el trabajo en la tabla hasta registrar un procesador real para su tipo.
                logger.LogWarning("No generation handlers are registered; queued jobs will remain available for a later worker.");
                _reportedMissingHandlers = true;
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
            return;
        }

        _reportedMissingHandlers = false;
        var duplicateHandlerTypes = handlers
            .GroupBy(handler => handler.JobType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateHandlerTypes.Length > 0)
        {
            if (!_reportedDuplicateHandlers)
            {
                logger.LogError(
                    "More than one handler is registered for job types {JobTypes}; queue polling is paused.",
                    string.Join(", ", duplicateHandlerTypes));
                _reportedDuplicateHandlers = true;
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
            return;
        }

        _reportedDuplicateHandlers = false;
        var repository = scope.ServiceProvider.GetRequiredService<IGenerationJobRepository>();
        var supportedTypes = handlers.Select(handler => handler.JobType).Distinct().ToArray();
        var claim = await repository.ClaimNextAsync(
            _workerId,
            supportedTypes,
            _options.LeaseDuration,
            stoppingToken);

        if (claim is null)
        {
            await Task.Delay(_options.PollInterval, stoppingToken);
            return;
        }

        var handler = handlers.Single(candidate => candidate.JobType == claim.Job.JobType);
        await ExecuteClaimAsync(claim, handler, stoppingToken);
    }

    private async Task ExecuteClaimAsync(
        ClaimedGenerationJob claim,
        IGenerationJobHandler handler,
        CancellationToken stoppingToken)
    {
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatTask = RenewLeaseUntilStoppedAsync(claim, executionCancellation, heartbeatCancellation.Token);
        var workItem = new GenerationJobWorkItem(
            claim.Job.Id,
            claim.Job.MusicProjectId,
            claim.Job.JobType,
            claim.Job.RequestedParametersJson,
            claim.Job.AttemptCount,
            (progress, token) => ReportProgressAsync(claim, progress, token));

        try
        {
            await handler.ExecuteAsync(workItem, executionCancellation.Token);

            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (executionCancellation.IsCancellationRequested)
            {
                var stopReason = await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
                if (stopReason == LeaseRenewalStatus.CancellationRequested)
                {
                    await MarkCancelledAsync(claim, stoppingToken);
                }
                else if (stopReason == LeaseRenewalStatus.LeaseLost)
                {
                    logger.LogWarning("Job {JobId} stopped after losing its lease; no state change was written.", claim.Job.Id);
                }

                return;
            }

            var completed = await WithRepositoryAsync(
                repository => repository.CompleteAsync(claim.Job.Id, claim.LeaseToken, stoppingToken),
                stoppingToken);
            if (!completed)
            {
                var leaseStatus = await WithRepositoryAsync(
                    repository => repository.RenewLeaseAsync(
                        claim.Job.Id,
                        claim.LeaseToken,
                        _options.LeaseDuration,
                        stoppingToken),
                    stoppingToken);
                if (leaseStatus == LeaseRenewalStatus.CancellationRequested)
                {
                    await MarkCancelledAsync(claim, stoppingToken);
                }
                else
                {
                    logger.LogWarning("Job {JobId} finished after its lease was lost; its result was not committed.", claim.Job.Id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Al cerrar el proceso dejamos vencer el lease; otro Worker recuperará el trabajo.
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            var stopReason = await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
            if (stopReason == LeaseRenewalStatus.CancellationRequested)
            {
                await MarkCancelledAsync(claim, CancellationToken.None);
            }
            else if (stopReason == LeaseRenewalStatus.LeaseLost)
            {
                logger.LogWarning("Job {JobId} stopped after losing its lease; no state change was written.", claim.Job.Id);
            }
        }
        catch (GenerationJobExecutionException exception)
        {
            await RecordFailureAsync(claim, exception.ErrorCode, exception.SafeMessage, exception.IsTransient, stoppingToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Job {JobId} failed with {ExceptionType}.",
                claim.Job.Id,
                exception.GetType().Name);
            await RecordFailureAsync(
                claim,
                "worker_unexpected_error",
                "The worker could not complete this job.",
                isTransient: true,
                stoppingToken);
        }
        finally
        {
            _ = await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
        }
    }

    private async Task<LeaseRenewalStatus?> RenewLeaseUntilStoppedAsync(
        ClaimedGenerationJob claim,
        CancellationTokenSource executionCancellation,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromTicks(_options.LeaseDuration.Ticks / 3);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            LeaseRenewalStatus status;
            try
            {
                status = await WithRepositoryAsync(
                    repository => repository.RenewLeaseAsync(
                        claim.Job.Id,
                        claim.LeaseToken,
                        _options.LeaseDuration,
                        cancellationToken),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Lease renewal for job {JobId} failed ({ExceptionType}); stopping local execution.",
                    claim.Job.Id,
                    exception.GetType().Name);
                await executionCancellation.CancelAsync();
                return LeaseRenewalStatus.LeaseLost;
            }

            if (status == LeaseRenewalStatus.Renewed)
            {
                continue;
            }

            if (status == LeaseRenewalStatus.CancellationRequested)
            {
                logger.LogInformation("Cancellation requested for running job {JobId}.", claim.Job.Id);
            }
            else
            {
                logger.LogWarning("Worker lost the lease for job {JobId}; stopping its local execution.", claim.Job.Id);
            }

            await executionCancellation.CancelAsync();
            return status;
        }

        return null;
    }

    private async Task ReportProgressAsync(
        ClaimedGenerationJob claim,
        int progressPercent,
        CancellationToken cancellationToken)
    {
        var updated = await WithRepositoryAsync(
            repository => repository.ReportProgressAsync(
                claim.Job.Id,
                claim.LeaseToken,
                progressPercent,
                cancellationToken),
            cancellationToken);

        if (!updated)
        {
            throw new OperationCanceledException("The job lease is no longer active.", cancellationToken);
        }
    }

    private async Task MarkCancelledAsync(ClaimedGenerationJob claim, CancellationToken cancellationToken)
    {
        var marked = await WithRepositoryAsync(
            repository => repository.MarkCancelledAsync(claim.Job.Id, claim.LeaseToken, cancellationToken),
            cancellationToken);
        if (!marked)
        {
            logger.LogWarning("Cancellation for job {JobId} was not committed because its lease changed.", claim.Job.Id);
        }
    }

    private async Task RecordFailureAsync(
        ClaimedGenerationJob claim,
        string errorCode,
        string safeMessage,
        bool isTransient,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var result = await WithRepositoryAsync(
            repository => repository.RecordFailureAsync(
                claim.Job.Id,
                claim.LeaseToken,
                errorCode,
                safeMessage,
                isTransient,
                retryPolicy.GetDelay(claim.Job.AttemptCount),
                cancellationToken),
            cancellationToken);

        if (result == FailureHandlingResult.CancellationRequested)
        {
            await MarkCancelledAsync(claim, cancellationToken);
        }
    }

    private async Task<TResult> WithRepositoryAsync<TResult>(
        Func<IGenerationJobRepository, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenerationJobRepository>();
        return await operation(repository);
    }

    private static async Task<LeaseRenewalStatus?> StopHeartbeatAsync(
        CancellationTokenSource heartbeatCancellation,
        Task<LeaseRenewalStatus?> heartbeatTask)
    {
        await heartbeatCancellation.CancelAsync();
        try
        {
            return await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // La cancelación del heartbeat es el cierre normal cuando acaba la tarea.
            return null;
        }
    }

    private static GenerationQueueOptions Validate(GenerationQueueOptions options)
    {
        options.Validate();
        return options;
    }
}
