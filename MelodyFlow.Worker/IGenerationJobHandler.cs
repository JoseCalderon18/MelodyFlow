using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Worker;

public interface IGenerationJobHandler
{
    JobType JobType { get; }

    Task ExecuteAsync(GenerationJobWorkItem job, CancellationToken cancellationToken);
}

public sealed class GenerationJobWorkItem(
    Guid id,
    Guid musicProjectId,
    JobType jobType,
    string requestedParametersJson,
    int attemptCount,
    Func<int, CancellationToken, Task> reportProgressAsync)
{
    public Guid Id { get; } = id;
    public Guid MusicProjectId { get; } = musicProjectId;
    public JobType JobType { get; } = jobType;
    public string RequestedParametersJson { get; } = requestedParametersJson;
    public int AttemptCount { get; } = attemptCount;

    public Task ReportProgressAsync(int progressPercent, CancellationToken cancellationToken = default) =>
        reportProgressAsync(progressPercent, cancellationToken);
}

public sealed class GenerationJobExecutionException(
    string errorCode,
    string safeMessage,
    bool isTransient,
    Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public string ErrorCode { get; } = errorCode;
    public string SafeMessage { get; } = safeMessage;
    public bool IsTransient { get; } = isTransient;
}
