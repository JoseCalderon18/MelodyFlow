namespace MelodyFlow.Application.Jobs;

/// <summary>
/// Keeps automatic retries bounded and gives the database queue a predictable delay.
/// </summary>
public sealed class GenerationJobRetryPolicy
{
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(5);

    public TimeSpan GetDelay(int completedAttemptCount)
    {
        if (completedAttemptCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAttemptCount),
                "A retry delay can only be calculated after an attempt.");
        }

        var multiplier = Math.Pow(2, Math.Min(completedAttemptCount - 1, 10));
        var delay = TimeSpan.FromSeconds(5 * multiplier);
        return delay <= MaximumDelay ? delay : MaximumDelay;
    }
}
