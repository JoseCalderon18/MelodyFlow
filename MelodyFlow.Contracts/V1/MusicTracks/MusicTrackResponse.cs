using System.Text.Json.Serialization;

namespace MelodyFlow.Contracts.V1.MusicTracks;

/// <summary>
/// Datos públicos de una canción generada; AudioUrl apunta al endpoint que transmite el archivo.
/// </summary>
public sealed record MusicTrackResponse(
    Guid Id,
    Guid? MusicProjectId,
    Guid? GenerationJobId,
    string Title,
    long? DurationMs,
    string? Format,
    string AudioUrl,
    DateTimeOffset CreatedAt,
    [property: JsonPropertyName("usedAI")] bool UsedAI);
