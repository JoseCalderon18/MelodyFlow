using System.ComponentModel.DataAnnotations;
using MelodyFlow.Contracts.Validation;

namespace MelodyFlow.Contracts.V1.Projects;

/// <summary>Campos que el cliente puede enviar para crear un proyecto musical.</summary>
public sealed class CreateMusicProjectRequest
{
    [Required]
    [NotWhiteSpace]
    [StringLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [StringLength(80)]
    public string Genre { get; init; } = string.Empty;

    [StringLength(80)]
    public string? Mood { get; init; }

    [Range(1, 3_600)]
    public int TargetDurationSeconds { get; init; }
}
