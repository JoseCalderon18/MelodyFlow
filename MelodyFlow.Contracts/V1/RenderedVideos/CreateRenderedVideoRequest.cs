using System.ComponentModel.DataAnnotations;

namespace MelodyFlow.Contracts.V1.RenderedVideos;

/// <summary>Solicita a la cola ensamblar un MP4 con una lista ordenada de canciones y un clip.</summary>
public sealed class CreateRenderedVideoRequest : IValidatableObject
{
    [Required]
    public Guid MusicProjectId { get; init; }

    /// <summary>Identificadores en el orden en que se escucharán. Si se omite, se acepta MusicTrackId por compatibilidad.</summary>
    [MaxLength(20)]
    public IReadOnlyList<Guid>? MusicTrackIds { get; init; }

    /// <summary>Campo legado para clientes que aún envían una sola canción.</summary>
    public Guid? MusicTrackId { get; init; }

    [Required]
    public Guid VisualAssetId { get; init; }

    /// <summary>Duración final del vídeo en segundos; si se omite, se usa la duración combinada de las canciones.</summary>
    [Range(1, 7_200)]
    public int? TargetDurationSeconds { get; init; }

    [StringLength(200)]
    public string? Title { get; init; }

    public IReadOnlyList<Guid> ResolveMusicTrackIds() => MusicTrackIds is { Count: > 0 }
        ? MusicTrackIds
        : MusicTrackId is Guid legacyTrackId ? [legacyTrackId] : [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trackIds = ResolveMusicTrackIds();
        if (trackIds.Count is < 1 or > 20)
        {
            yield return new ValidationResult("Selecciona entre 1 y 20 canciones.", [nameof(MusicTrackIds)]);
            yield break;
        }

        if (trackIds.Any(trackId => trackId == Guid.Empty) || trackIds.Distinct().Count() != trackIds.Count)
        {
            yield return new ValidationResult("Las canciones seleccionadas deben ser válidas y no repetirse.", [nameof(MusicTrackIds)]);
        }
    }
}
