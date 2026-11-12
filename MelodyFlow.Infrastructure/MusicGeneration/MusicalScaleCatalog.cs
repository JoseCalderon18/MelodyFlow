namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Escalas heptatónicas que MelodyFlow puede armonizar y convertir a MIDI.</summary>
internal sealed record MusicalScaleDefinition(string Name, IReadOnlyList<int> Semitones, bool MinorColor)
{
    public int GetMidiNote(int rootPitchClass, int degree, int octaveOffset = 0)
    {
        var scaleLength = Semitones.Count;
        var octave = (int)Math.Floor(degree / (double)scaleLength);
        var scaleDegree = ((degree % scaleLength) + scaleLength) % scaleLength;
        return 60 + rootPitchClass + Semitones[scaleDegree] + ((octave + octaveOffset) * 12);
    }

    public string BuildDiatonicTriad(int rootPitchClass, int degree)
    {
        const int degreesPerOctave = 7;
        var normalizedDegree = ((degree % degreesPerOctave) + degreesPerOctave) % degreesPerOctave;
        var root = Semitones[normalizedDegree];
        var thirdIndex = normalizedDegree + 2;
        var fifthIndex = normalizedDegree + 4;
        var third = Semitones[thirdIndex % degreesPerOctave] + (thirdIndex >= degreesPerOctave ? 12 : 0);
        var fifth = Semitones[fifthIndex % degreesPerOctave] + (fifthIndex >= degreesPerOctave ? 12 : 0);
        var thirdInterval = third - root;
        var fifthInterval = fifth - root;
        var quality = (thirdInterval, fifthInterval) switch
        {
            (4, 7) => string.Empty,
            (3, 7) => "m",
            (3, 6) => "dim",
            (4, 8) => "aug",
            _ => throw new InvalidDataException($"Scale '{Name}' produced an unsupported diatonic triad.")
        };

        var pitchClass = (rootPitchClass + root) % 12;
        return MusicalScaleCatalog.PitchClassNames[pitchClass] + quality;
    }
}

/// <summary>
/// Twelve chromatic roots combined with nine seven-note modes/scales provide 108
/// root-scale choices. These are combinations, not 108 distinct key signatures.
/// </summary>
internal static class MusicalScaleCatalog
{
    internal static readonly string[] PitchClassNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly MusicalScaleDefinition[] Definitions =
    [
        new("ionian", [0, 2, 4, 5, 7, 9, 11], MinorColor: false),
        new("dorian", [0, 2, 3, 5, 7, 9, 10], MinorColor: true),
        new("phrygian", [0, 1, 3, 5, 7, 8, 10], MinorColor: true),
        new("lydian", [0, 2, 4, 6, 7, 9, 11], MinorColor: false),
        new("mixolydian", [0, 2, 4, 5, 7, 9, 10], MinorColor: false),
        new("aeolian", [0, 2, 3, 5, 7, 8, 10], MinorColor: true),
        new("locrian", [0, 1, 3, 5, 6, 8, 10], MinorColor: true),
        new("harmonic-minor", [0, 2, 3, 5, 7, 8, 11], MinorColor: true),
        new("melodic-minor", [0, 2, 3, 5, 7, 9, 11], MinorColor: true)
    ];

    public static IReadOnlyList<MusicalScaleDefinition> All => Definitions;

    public static bool TryGet(string? name, out MusicalScaleDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static MusicalScaleDefinition GetRequired(string name) =>
        TryGet(name, out var definition)
            ? definition
            : throw new InvalidDataException("The arrangement uses an unsupported musical scale.");

    public static string DefaultFor(bool isMinor) => isMinor ? "aeolian" : "ionian";

    public static IReadOnlyList<string> SelectFor(string genre, string? mood)
    {
        var context = $"{genre} {mood}".ToLowerInvariant();
        if (ContainsAny(context, "dark", "oscuro", "tension", "mister", "phrygian"))
        {
            return ["phrygian", "locrian", "harmonic-minor", "aeolian"];
        }

        if (ContainsAny(context, "hope", "uplift", "optim", "luz", "bright", "dream", "lydian"))
        {
            return ["lydian", "ionian", "mixolydian", "dorian"];
        }

        if (ContainsAny(context, "pop", "rock", "latin", "rnb", "energetic", "energ", "upbeat"))
        {
            return ["ionian", "mixolydian", "dorian", "aeolian", "lydian", "melodic-minor"];
        }

        if (ContainsAny(context, "ambient", "lofi", "lo-fi", "calm", "relax", "sleep", "melanch", "sad", "triste"))
        {
            return ["dorian", "aeolian", "lydian", "mixolydian", "harmonic-minor", "ionian"];
        }

        if (ContainsAny(context, "cinematic", "orchestral", "epic", "film"))
        {
            return ["dorian", "aeolian", "lydian", "phrygian", "harmonic-minor", "ionian"];
        }

        return Definitions.Select(definition => definition.Name).ToArray();
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
