namespace MelodyFlow.Worker;

public sealed class GenerationQueueOptions
{
    public int PollIntervalMilliseconds { get; init; } = 2_000;
    public int LeaseDurationSeconds { get; init; } = 90;

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(PollIntervalMilliseconds);
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);

    public void Validate()
    {
        if (PollIntervalMilliseconds is < 250 or > 60_000)
        {
            throw new InvalidOperationException("Queue:PollIntervalMilliseconds must be between 250 and 60000.");
        }

        if (LeaseDurationSeconds is < 15 or > 3_600)
        {
            throw new InvalidOperationException("Queue:LeaseDurationSeconds must be between 15 and 3600.");
        }
    }
}
