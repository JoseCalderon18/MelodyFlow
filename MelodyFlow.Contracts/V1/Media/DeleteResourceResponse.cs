namespace MelodyFlow.Contracts.V1.Media;

/// <summary>Confirms deletion and reports if a local media file needs manual cleanup.</summary>
public sealed record DeleteResourceResponse(bool Deleted, string? Warning);
