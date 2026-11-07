using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MelodyFlow.Application.Projects;
using MelodyFlow.Domain.Entities;
using MelodyFlow.Domain.Enums;

namespace MelodyFlow.Application.Jobs;

/// <summary>Orquesta la creación idempotente, consulta y cancelación de trabajos de generación.</summary>
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
        // Verificar el proyecto antes de crear la fila evita encolar referencias inexistentes.
        if (await projects.GetByIdAsync(musicProjectId, cancellationToken) is null)
        {
            return new CreateGenerationJobResult(null, CreateGenerationJobOutcome.ProjectNotFound);
        }

        // La huella permite distinguir un reintento igual de una clave repetida con parámetros distintos.
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

    // El orden de propiedades y los espacios no deben hacer que dos reintentos idénticos entren en conflicto.
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

/// <summary>Resultado de crear o reutilizar un trabajo de generación.</summary>
public enum CreateGenerationJobOutcome
{
    ProjectNotFound,
    Created,
    ExistingRequest,
    IdempotencyConflict
}

/// <summary>Trabajo resultante y estado de la operación de creación.</summary>
public sealed record CreateGenerationJobResult(GenerationJob? Job, CreateGenerationJobOutcome Outcome);
