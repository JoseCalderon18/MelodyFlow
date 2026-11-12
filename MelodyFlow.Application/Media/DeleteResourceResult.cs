namespace MelodyFlow.Application.Media;

/// <summary>Outcome of removing a media record and its file from local storage.</summary>
public enum DeleteResourceOutcome
{
    Deleted,
    NotFound,
    InUse,
    FileCleanupFailed
}

/// <summary>Includes the storage key only inside the application boundary for post-delete cleanup.</summary>
public sealed record DeleteResourceResult(DeleteResourceOutcome Outcome, string? StorageKey = null);
