using MelodyFlow.Application.Music;
using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Builds repeatable, style-aware plans without calling a generative model.</summary>
internal static class DeterministicArrangementPlanner
{
    private static readonly IReadOnlyDictionary<string, int[][]> Progressions = new Dictionary<string, int[][]>(StringComparer.OrdinalIgnoreCase)
    {
        ["ionian"] = [[0, 4, 5, 3], [0, 5, 3, 4], [0, 3, 4, 0]],
        ["dorian"] = [[0, 3, 6, 4], [0, 4, 3, 6], [0, 6, 4, 0]],
        ["phrygian"] = [[0, 6, 5, 0], [0, 1, 5, 0], [0, 6, 3, 6]],
        ["lydian"] = [[0, 4, 1, 5], [0, 5, 4, 0], [0, 1, 4, 5]],
        ["mixolydian"] = [[0, 6, 3, 0], [0, 4, 6, 3], [0, 5, 4, 0]],
        ["aeolian"] = [[0, 5, 2, 6], [0, 6, 5, 6], [0, 3, 6, 5]],
        ["locrian"] = [[0, 1, 5, 0], [0, 4, 1, 5], [0, 5, 4, 1]],
        ["harmonic-minor"] = [[0, 5, 6, 0], [0, 3, 4, 0], [0, 6, 5, 0]],
        ["melodic-minor"] = [[0, 4, 3, 6], [0, 5, 2, 0], [0, 3, 4, 6]]
    };

    public static ArrangementPlan Create(MusicProject project, MusicGenerationRequest request, int seed)
    {
        var selectedKey = request.PreferredKey;
        var root = selectedKey is not null && MusicKey.TryParseRoot(selectedKey, out var preferredRoot)
            ? preferredRoot
            : unchecked((int)((uint)seed % 12));
        var parsedKey = selectedKey is not null && MusicKey.TryParse(selectedKey, out var fullKey)
            ? fullKey
            : (MusicKey?)null;

        var scaleName = request.PreferredScale
            ?? (parsedKey is not null
                ? MusicalScaleCatalog.DefaultFor(parsedKey.Value.IsMinor)
                : SelectScale(project, seed));
        var scale = MusicalScaleCatalog.GetRequired(scaleName);
        var keyName = $"{MusicalScaleCatalog.PitchClassNames[root]} {(scale.MinorColor ? "minor" : "major")}";
        var tempo = request.PreferredTempoBpm ?? SelectTempo(project, seed);
        var style = ArrangementStyleCatalog.ForProject(project);
        var positiveSeed = unchecked((uint)seed);
        var patterns = Progressions[scale.Name];
        var firstPatternIndex = (int)(positiveSeed % (uint)patterns.Length);
        var secondPatternIndex = (int)((positiveSeed >> 4) % (uint)patterns.Length);
        if (secondPatternIndex == firstPatternIndex)
        {
            secondPatternIndex = (secondPatternIndex + 1) % patterns.Length;
        }

        var firstRotation = (int)((positiveSeed >> 8) % 4);
        var secondRotation = (int)((positiveSeed >> 12) % 4);
        var chordDegrees = patterns[firstPatternIndex].Skip(firstRotation)
            .Concat(patterns[firstPatternIndex].Take(firstRotation))
            .Concat(patterns[secondPatternIndex].Skip(secondRotation))
            .Concat(patterns[secondPatternIndex].Take(secondRotation));
        var chords = chordDegrees.Select(degree => scale.BuildDiatonicTriad(root, degree)).ToArray();

        return new ArrangementPlan(1, tempo, keyName, chords, seed)
        {
            UsedAI = false,
            Scale = scale.Name,
            Groove = style.Groove,
            InstrumentSet = style.InstrumentSet
        };
    }

    private static string SelectScale(MusicProject project, int seed)
    {
        var candidates = MusicalScaleCatalog.SelectFor(project.Genre, project.Mood);
        var index = (int)(unchecked((uint)seed >> 2) % (uint)candidates.Count);
        return candidates[index];
    }

    private static int SelectTempo(MusicProject project, int seed)
    {
        var genre = project.Genre.ToLowerInvariant();
        var mood = project.Mood?.ToLowerInvariant() ?? string.Empty;
        var offset = (int)(unchecked((uint)seed >> 5) % 13);
        if (ContainsAny(mood, "energetic", "upbeat", "intense", "energético", "animado"))
        {
            return 108 + offset;
        }

        if (ContainsAny(mood, "calm", "chill", "relax", "calmado", "tranquilo", "sleep"))
        {
            return 66 + offset;
        }

        if (ContainsAny(genre, "house", "dance", "techno", "electronic"))
        {
            return 108 + offset;
        }

        if (ContainsAny(genre, "lofi", "lo-fi", "ambient", "classical", "cinematic"))
        {
            return 70 + offset;
        }

        if (ContainsAny(genre, "rock", "metal", "punk", "pop"))
        {
            return 92 + offset;
        }

        return 82 + offset;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
