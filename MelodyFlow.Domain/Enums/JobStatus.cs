namespace MelodyFlow.Domain.Enums;

/// <summary>Estados persistidos de una solicitud de generación y del Worker que la ejecuta.</summary>
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
