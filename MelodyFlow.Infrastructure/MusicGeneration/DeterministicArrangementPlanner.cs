using MelodyFlow.Application.Music;
using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Genera acordes y parámetros básicos con reglas reproducibles, sin llamar a ningún modelo.</summary>
internal static class DeterministicArrangementPlanner
{
    private static readonly string[] PitchClassNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly int[][] MajorProgressions = [[0, 4, 5, 3], [0, 5, 3, 4], [0, 3, 4, 0]];
    private static readonly int[][] MinorProgressions = [[0, 5, 2, 6], [0, 6, 5, 6], [0, 3, 6, 5]];
    private static readonly string[] MajorQualities = ["", "m", "m", "", "", "m", "dim"];
    private static readonly string[] MinorQualities = ["m", "dim", "", "m", "m", "", ""];

    public static ArrangementPlan Create(MusicProject project, MusicGenerationRequest request, int seed)
    {
        var keyName = request.PreferredKey ?? SelectKey(project, seed);
        if (!MusicKey.TryParse(keyName, out var key))
        {
            throw new MusicGenerationException(
                "unsupported_music_preferences",
                "The selected key is not supported.",
                isTransient: false);
        }

        var tempo = request.PreferredTempoBpm ?? SelectTempo(project);
        var progressions = key.IsMinor ? MinorProgressions : MajorProgressions;
        var qualities = key.IsMinor ? MinorQualities : MajorQualities;
        var positiveSeed = unchecked((uint)seed);
        var progression = progressions[(int)(positiveSeed % (uint)progressions.Length)];
        var rotation = (int)((positiveSeed >> 3) % (uint)progression.Length);
        var chords = Enumerable.Range(0, progression.Length)
            .Select(index =>
            {
                var degree = progression[(index + rotation) % progression.Length];
                var root = (key.RootPitchClass + key.Scale[degree]) % 12;
                return PitchClassNames[root] + qualities[degree];
            })
            .ToArray();

        return new ArrangementPlan(1, tempo, keyName, chords, seed)
        {
            UsedAI = false
        };
    }

    private static string SelectKey(MusicProject project, int seed)
    {
        var context = $"{project.Genre} {project.Mood}".ToLowerInvariant();
        var preferMinor = ContainsAny(context, "lofi", "ambient", "calm", "chill", "sad", "dark", "relax", "triste", "melancol");
        var root = (int)(unchecked((uint)seed) % 12);
        return $"{PitchClassNames[root]} {(preferMinor ? "minor" : "major")}";
    }

    private static int SelectTempo(MusicProject project)
    {
        var genre = project.Genre.ToLowerInvariant();
        var mood = project.Mood?.ToLowerInvariant() ?? string.Empty;
        if (ContainsAny(mood, "energetic", "upbeat", "intense", "energético", "animado"))
        {
            return 116;
        }

        if (ContainsAny(genre, "lofi", "ambient", "classical") || ContainsAny(mood, "calm", "chill", "relax", "calmado", "tranquilo"))
        {
            return 78;
        }

        if (ContainsAny(genre, "dance", "electronic", "house", "techno"))
        {
            return 120;
        }

        if (ContainsAny(genre, "rock", "metal", "punk"))
        {
            return 112;
        }

        return 92;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
