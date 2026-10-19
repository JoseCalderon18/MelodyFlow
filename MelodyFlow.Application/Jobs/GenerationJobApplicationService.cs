using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Jobs;

public sealed class GenerationJobApplicationService(
    IMusicProjectRepository projects,
    IGenerationJobRepository jobs)
{
    public async Task<CreateGenerationJobResult> CreateAsync(
        Guid musicProjectId,
        JobType jobType,
        string requestedParametersJson,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (await projects.GetByIdAsync(musicProjectId, cancellationToken) is null)
        {
            return new CreateGenerationJobResult(null, CreateGenerationJobOutcome.ProjectNotFound);
        }

        var fingerprint = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : CreateFingerprint(musicProjectId, jobType, requestedParametersJson);

        var job = new GenerationJob(
            musicProjectId,
            jobType,
            requestedParametersJson,
            idempotencyKey: idempotencyKey,
            requestFingerprint: fingerprint);
        job.Queue();

        var result = await jobs.EnqueueOrGetAsync(job, cancellationToken);
        return result.Outcome switch
        {
            EnqueueGenerationJobOutcome.Created =>
                new CreateGenerationJobResult(result.Job, CreateGenerationJobOutcome.Created),
            EnqueueGenerationJobOutcome.ExistingRequest =>
                new CreateGenerationJobResult(result.Job, CreateGenerationJobOutcome.ExistingRequest),
            _ => new CreateGenerationJobResult(result.Job, CreateGenerationJobOutcome.IdempotencyConflict)
        };
    }

    public Task<GenerationJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        jobs.GetByIdAsync(id, cancellationToken);

    public Task<CancellationGenerationJobResult> RequestCancellationAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        jobs.RequestCancellationAsync(id, cancellationToken);

    private static string CreateFingerprint(Guid musicProjectId, JobType jobType, string requestedParametersJson)
    {
        using var document = JsonDocument.Parse(requestedParametersJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("musicProjectId", musicProjectId);
            writer.WriteString("jobType", jobType.ToString());
            writer.WritePropertyName("requestedParameters");
            WriteCanonicalJson(writer, document.RootElement);
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    // Property order and whitespace should not make two otherwise identical n8n retries conflict.
    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}

public enum CreateGenerationJobOutcome
{
    ProjectNotFound,
    Created,
    ExistingRequest,
    IdempotencyConflict
}

public sealed record CreateGenerationJobResult(GenerationJob? Job, CreateGenerationJobOutcome Outcome);
