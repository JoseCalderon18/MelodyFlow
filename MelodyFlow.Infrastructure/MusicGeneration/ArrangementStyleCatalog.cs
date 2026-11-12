using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Infrastructure.MusicGeneration;

internal sealed record ArrangementStyle(string Groove, string InstrumentSet);

/// <summary>Maps the user's style to MIDI patterns and General MIDI program families.</summary>
internal static class ArrangementStyleCatalog
{
    private static readonly string[] Grooves = ["ambient", "lofi", "backbeat", "four-on-floor", "cinematic"];
    private static readonly string[] InstrumentSets = ["ambient", "lofi", "pop", "electronic", "cinematic", "rock", "acoustic"];

    public static ArrangementStyle ForProject(MusicProject project)
    {
        var genre = project.Genre.ToLowerInvariant();
        if (ContainsAny(genre, "ambient", "drone", "meditation", "sleep"))
        {
            return new ArrangementStyle("ambient", "ambient");
        }

        if (ContainsAny(genre, "lofi", "lo-fi", "chillhop"))
        {
            return new ArrangementStyle("lofi", "lofi");
        }

        if (ContainsAny(genre, "pop", "rnb", "r&b", "latin", "hip-hop"))
        {
            return new ArrangementStyle("backbeat", "pop");
        }

        if (ContainsAny(genre, "house", "dance", "techno", "electronic", "synthwave", "synth"))
        {
            return new ArrangementStyle("four-on-floor", "electronic");
        }

        if (ContainsAny(genre, "cinematic", "orchestral", "classical", "soundtrack"))
        {
            return new ArrangementStyle("cinematic", "cinematic");
        }

        if (ContainsAny(genre, "rock", "metal", "punk"))
        {
            return new ArrangementStyle("backbeat", "rock");
        }

        return new ArrangementStyle("backbeat", "acoustic");
    }

    public static bool IsSupportedGroove(string value) => Grooves.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedInstrumentSet(string value) => InstrumentSets.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
