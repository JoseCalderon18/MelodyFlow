using System.ComponentModel.DataAnnotations;

namespace MelodyFlow.Contracts.V1.RenderedVideos;

/// <summary>Solicita a la cola ensamblar un MP4 con una canción y un clip existentes.</summary>
public sealed class CreateRenderedVideoRequest
{
    [Required]
    public Guid MusicProjectId { get; init; }

    [Required]
    public Guid MusicTrackId { get; init; }

    [Required]
    public Guid VisualAssetId { get; init; }

    [StringLength(200)]
    public string? Title { get; init; }
}
