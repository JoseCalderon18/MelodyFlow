using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace MelodyFlow.Contracts.V1.GenerationJobs;

public sealed class CreateGenerationJobRequest : IValidatableObject
{
    public Guid MusicProjectId { get; init; }

    public string JobType { get; init; } = "Music";

    /// <summary>
    /// Optional, non-secret options for the requested job. The project supplies its core music brief.
    /// </summary>
    public JsonElement? RequestedParameters { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MusicProjectId == Guid.Empty)
        {
            yield return new ValidationResult("A music project ID is required.", [nameof(MusicProjectId)]);
        }

        if (!string.Equals(JobType, "Music", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Only Music jobs are currently supported.",
                [nameof(JobType)]);
        }

        if (RequestedParameters is { } parameters)
        {
            if (parameters.ValueKind != JsonValueKind.Object)
            {
                yield return new ValidationResult(
                    "Requested parameters must be a JSON object.",
                    [nameof(RequestedParameters)]);
            }
            else if (Encoding.UTF8.GetByteCount(parameters.GetRawText()) > 65_536)
            {
                yield return new ValidationResult(
                    "Requested parameters cannot exceed 64 KB.",
                    [nameof(RequestedParameters)]);
            }
        }
    }
}
