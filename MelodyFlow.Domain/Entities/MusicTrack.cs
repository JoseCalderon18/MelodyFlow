using System.Text.Json;

namespace MelodyFlow.Domain.Entities;

/// <summary>
/// Archivo de audio generado y sus metadatos; la canción puede reutilizarse en otros proyectos.
/// </summary>
/// <remarks>
/// La entidad valida sus invariantes de dominio al construirse; EF Core usa el constructor privado
/// al materializar filas existentes. El archivo se referencia con una clave, nunca se guarda aquí.
/// </remarks>
public sealed class MusicTrack
{
    public const int MaxTitleLength = 200;
    public const int MaxStorageKeyLength = 1_024;
    public const int MaxFormatLength = 16;
    public const int MaxProviderAssetIdLength = 200;
    public const int MaxLicenseEvidenceKeyLength = 1_024;

    private MusicTrack()
    {
    }

    /// <summary>
    /// Crea los metadatos de una canción y comprueba los valores que deben ser válidos siempre.
    /// </summary>
    public MusicTrack(
        Guid? musicProjectId,
        Guid? generationJobId,
        string title,
        long? durationMs,
        string storageKey,
        string? format,
        string? providerAssetId,
        string? licenseEvidenceKey,
        string arrangementPlanJson,
        bool usedAI)
    {
        if (musicProjectId == Guid.Empty)
        {
            throw new ArgumentException("A project ID must be a valid GUID when supplied.", nameof(musicProjectId));
        }

        if (generationJobId == Guid.Empty)
        {
            throw new ArgumentException("A generation job ID must be a valid GUID when supplied.", nameof(generationJobId));
        }

        if (durationMs is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMs), durationMs, "Track duration must be positive when supplied.");
        }

        Id = Guid.NewGuid();
        MusicProjectId = musicProjectId;
        GenerationJobId = generationJobId;
        Title = RequiredText(title, nameof(title), MaxTitleLength);
        DurationMs = durationMs;
        StorageKey = ValidateStorageKey(storageKey);
        Format = OptionalText(format, nameof(format), MaxFormatLength)?.ToLowerInvariant();
        ProviderAssetId = OptionalText(providerAssetId, nameof(providerAssetId), MaxProviderAssetIdLength);
        LicenseEvidenceKey = OptionalText(licenseEvidenceKey, nameof(licenseEvidenceKey), MaxLicenseEvidenceKeyLength);
        ArrangementPlanJson = ValidateArrangementPlan(arrangementPlanJson);
        UsedAI = usedAI;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid? MusicProjectId { get; private set; }
    public Guid? GenerationJobId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public long? DurationMs { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string? Format { get; private set; }
    public string? ProviderAssetId { get; private set; }
    public string? LicenseEvidenceKey { get; private set; }
    public string ArrangementPlanJson { get; private set; } = "{}";
    public bool UsedAI { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static string ValidateStorageKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        var segments = normalized.Split('/');

        // La clave debe ser relativa y estar formada por segmentos normales para evitar rutas externas.
        if (normalized.Length > MaxStorageKeyLength ||
            normalized.StartsWith('/') ||
            normalized.Contains('\\') ||
            normalized.Contains(':') ||
            segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Storage key must be a safe relative path.", nameof(value));
        }

        return normalized;
    }

    private static string ValidateArrangementPlan(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Arrangement plan must be a JSON object.", nameof(value));
            }

            // Se conserva una representación JSON normalizada para guardar el plan usado en la generación.
            var normalized = document.RootElement.GetRawText();
            if (System.Text.Encoding.UTF8.GetByteCount(normalized) > 65_536)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Arrangement plan cannot exceed 64 KB.");
            }

            return normalized;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Arrangement plan must contain valid JSON.", nameof(value), exception);
        }
    }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, normalized.Length, $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? OptionalText(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, normalized.Length, $"Text cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
