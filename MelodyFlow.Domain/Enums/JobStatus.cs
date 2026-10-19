namespace MelodyFlow.Domain.Enums;

public enum JobStatus
{
    Pending,
    Queued,
    Running,
    Retrying,
    Cancelling,
    Completed,
    Failed,
    Cancelled
}
