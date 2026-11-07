using System.Net;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Configuración local de renderizado y del planificador de IA opcional.</summary>
public sealed class MusicGenerationOptions
{
    public PlannerOptions Planner { get; init; } = new();
    public string SoundFontPath { get; init; } = string.Empty;
    public string SoundFontLicenseEvidenceKey { get; init; } = string.Empty;
    public string FluidSynthPath { get; init; } = "fluidsynth";
    public string FfmpegPath { get; init; } = "ffmpeg";
    public string FfprobePath { get; init; } = "ffprobe";
    public int ProcessTimeoutSeconds { get; init; } = 3_600;

    /// <summary>Comprueba los requisitos antes de registrar el handler musical del Worker.</summary>
    public IReadOnlyList<string> GetConfigurationProblems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(SoundFontPath) || !File.Exists(SoundFontPath))
        {
            problems.Add("SoundFontPath must point to the locally downloaded FluidR3_GM.sf2 file.");
        }

        if (string.IsNullOrWhiteSpace(SoundFontLicenseEvidenceKey))
        {
            problems.Add("SoundFontLicenseEvidenceKey must identify the SoundFont license notice.");
        }

        if (!ExecutableResolver.Exists(FluidSynthPath))
        {
            problems.Add("FluidSynthPath must resolve to a FluidSynth executable.");
        }

        if (!ExecutableResolver.Exists(FfmpegPath))
        {
            problems.Add("FfmpegPath must resolve to an FFmpeg executable.");
        }

        if (!ExecutableResolver.Exists(FfprobePath))
        {
            problems.Add("FfprobePath must resolve to an FFprobe executable.");
        }

        if (ProcessTimeoutSeconds is < 30 or > 86_400)
        {
            problems.Add("ProcessTimeoutSeconds must be between 30 and 86400.");
        }

        return problems;
    }

    /// <summary>Comprueba LM Studio solo cuando el usuario activa el modo con IA.</summary>
    public IReadOnlyList<string> GetAiPlannerConfigurationProblems()
    {
        var problems = new List<string>();
        if (!IsLocalPlannerUrl(Planner.BaseUrl))
        {
            problems.Add("Planner:BaseUrl must be a local LM Studio HTTP endpoint.");
        }

        if (string.IsNullOrWhiteSpace(Planner.Model))
        {
            problems.Add("Planner:Model must name a model loaded in LM Studio.");
        }

        if (Planner.TimeoutSeconds is < 30 or > 3_600)
        {
            problems.Add("Planner:TimeoutSeconds must be between 30 and 3600.");
        }

        return problems;
    }

    private static bool IsLocalPlannerUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }
}

public sealed class PlannerOptions
{
    public string BaseUrl { get; init; } = "http://localhost:1234/v1";
    /// <summary>Nombre del modelo que debe estar cargado en el servidor local de LM Studio.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Opcional para LM Studio local; si se usa, guardar en User Secrets y nunca en appsettings.</summary>
    public string? ApiKey { get; init; }
    public int TimeoutSeconds { get; init; } = 600;
}

internal static class ExecutableResolver
{
    /// <summary>Indica si la ruta configurada resuelve a un archivo ejecutable disponible.</summary>
    public static bool Exists(string? configuredPath) => Resolve(configuredPath) is not null;

    /// <summary>Busca ejecutables en una ruta explícita o en los directorios de PATH.</summary>
    public static string? Resolve(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (Path.IsPathRooted(configuredPath) ||
            configuredPath.Contains(Path.DirectorySeparatorChar) ||
            configuredPath.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(configuredPath) ? Path.GetFullPath(configuredPath) : null;
        }

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';')
            : [string.Empty];
        var configuredHasExtension = Path.HasExtension(configuredPath);
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in configuredHasExtension ? [string.Empty] : extensions)
            {
                var candidate = Path.Combine(directory, configuredPath + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
