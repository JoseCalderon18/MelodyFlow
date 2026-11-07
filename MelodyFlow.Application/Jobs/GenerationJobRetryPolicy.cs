namespace MelodyFlow.Application.Jobs;

/// <summary>
/// Limita los reintentos automáticos y establece una espera predecible para la cola.
/// </summary>
public sealed class GenerationJobRetryPolicy
{
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromMinutes(5);

    /// <summary>Calcula un backoff exponencial desde cinco segundos, limitado a cinco minutos.</summary>
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
