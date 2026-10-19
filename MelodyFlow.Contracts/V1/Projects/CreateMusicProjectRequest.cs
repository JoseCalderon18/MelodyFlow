using System.ComponentModel.DataAnnotations;
using MelodyFlow.Contracts.Validation;

namespace MelodyFlow.Contracts.V1.Projects;

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

    [Range(1, int.MaxValue)]
    public int TargetDurationSeconds { get; init; }
}
