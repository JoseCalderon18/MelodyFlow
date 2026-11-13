using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Infrastructure.MusicGeneration;

internal sealed record ArrangementStyle(string Groove, string InstrumentSet, string Form, string TextureDensity, string MixProfile);

/// <summary>Maps the user's style to MIDI patterns and General MIDI program families.</summary>
internal static class ArrangementStyleCatalog
{
    private static readonly string[] Grooves =
    [
        "ambient", "lofi", "backbeat", "four-on-floor", "cinematic",
        "pulse", "arpeggiated", "shuffle", "swing", "broken-beat", "clave"
    ];
    private static readonly string[] InstrumentSets = ["ambient", "lofi", "pop", "electronic", "cinematic", "rock", "acoustic"];
    private static readonly string[] Forms = ["evolving-arc", "aaba", "build-break-return", "cinematic-arc", "steady-groove"];
    private static readonly string[] Densities = ["sparse", "balanced", "layered"];
    private static readonly string[] MixProfiles = ["intimate", "balanced", "spacious", "warm"];

    public static ArrangementStyle ForProject(MusicProject project, int seed, MusicGenerationRequest? request = null)
    {
        var genre = project.Genre.ToLowerInvariant();
        ArrangementStyle style;
        if (ContainsAny(genre, "ambient", "drone", "meditation", "sleep"))
        {
            style = new ArrangementStyle("ambient", "ambient", "evolving-arc", "sparse", "spacious");
        }
        else if (ContainsAny(genre, "lofi", "lo-fi", "chillhop"))
        {
            style = new ArrangementStyle("lofi", "lofi", "aaba", "balanced", "warm");
        }
        else if (ContainsAny(genre, "latin", "salsa", "reggaeton"))
        {
            style = new ArrangementStyle("clave", "pop", "build-break-return", "balanced", "balanced");
        }
        else if (ContainsAny(genre, "jazz", "swing", "bossa"))
        {
            style = new ArrangementStyle("swing", "acoustic", "aaba", "balanced", "warm");
        }
        else if (ContainsAny(genre, "house", "dance", "techno", "electronic", "synthwave", "synth"))
        {
            style = new ArrangementStyle("four-on-floor", "electronic", "build-break-return", "layered", "spacious");
        }
        else if (ContainsAny(genre, "cinematic", "orchestral", "classical", "soundtrack"))
        {
            style = new ArrangementStyle("cinematic", "cinematic", "cinematic-arc", "layered", "spacious");
        }
        else if (ContainsAny(genre, "rock", "metal", "punk"))
        {
            style = new ArrangementStyle("backbeat", "rock", "build-break-return", "layered", "balanced");
        }
        else if (ContainsAny(genre, "pop", "rnb", "r&b", "hip-hop"))
        {
            style = new ArrangementStyle("backbeat", "pop", "aaba", "balanced", "balanced");
        }
        else
        {
            style = new ArrangementStyle("backbeat", "acoustic", "evolving-arc", "balanced", "intimate");
        }

        var variant = (int)((unchecked((uint)seed) / 7) % 3);
        var alternateGrooves = style.Groove switch
        {
            "ambient" => new[] { "ambient", "pulse", "arpeggiated" },
            // Keep lofi as a valid seed-selected outcome while allowing the same genre to move
            // into related shuffle/broken-beat feels on other jobs.
            "lofi" => new[] { "shuffle", "broken-beat", "lofi" },
            "backbeat" => new[] { "backbeat", "shuffle", "broken-beat" },
            "four-on-floor" => new[] { "four-on-floor", "broken-beat", "pulse" },
            "cinematic" => new[] { "cinematic", "pulse", "arpeggiated" },
            "swing" => new[] { "swing", "shuffle", "backbeat" },
            "clave" => new[] { "clave", "backbeat", "broken-beat" },
            _ => new[] { style.Groove }
        };
        var alternateForms = style.Form switch
        {
            "evolving-arc" => new[] { "evolving-arc", "steady-groove", "aaba" },
            "aaba" => new[] { "aaba", "evolving-arc", "steady-groove" },
            "build-break-return" => new[] { "build-break-return", "steady-groove", "aaba" },
            "cinematic-arc" => new[] { "cinematic-arc", "evolving-arc", "build-break-return" },
            _ => new[] { style.Form }
        };

        var mood = project.Mood?.ToLowerInvariant() ?? string.Empty;
        var density = request?.TextureDensity ??
            (ContainsAny(mood, "sleep", "meditation", "calm", "tranquil", "focus", "relax")
                ? (variant == 2 ? "balanced" : "sparse")
                : variant == 0 ? style.TextureDensity : variant == 1 ? "balanced" : "layered");
        var mixProfile = request?.MixProfile ?? style.MixProfile;

        return style with
        {
            Groove = request?.PreferredGroove ?? alternateGrooves[variant % alternateGrooves.Length],
            Form = request?.PreferredForm ?? alternateForms[variant % alternateForms.Length],
            TextureDensity = density,
            MixProfile = mixProfile
        };
    }

    public static bool IsSupportedGroove(string value) => Grooves.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedInstrumentSet(string value) => InstrumentSets.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedForm(string value) => Forms.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedDensity(string value) => Densities.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedMixProfile(string value) => MixProfiles.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
