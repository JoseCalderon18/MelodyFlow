using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Plan musical que el compositor convierte después en MIDI.</summary>
internal sealed record ArrangementPlan(
    int SchemaVersion,
    int TempoBpm,
    string Key,
    IReadOnlyList<string> Chords,
    int VariationSeed)
{
    [JsonPropertyName("usedAI")]
    public bool UsedAI { get; init; }
}

internal static partial class ArrangementPlanParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Lee JSON estricto y rechaza planes que el compositor todavía no sabe representar.</summary>
    public static ArrangementPlan Parse(string json)
    {
        ArrangementPlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<ArrangementPlan>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The planner response did not match arrangement schema version 1.", exception);
        }

        if (plan is null || plan.SchemaVersion != 1)
        {
            throw new InvalidDataException("The planner must return arrangement schema version 1.");
        }

        if (plan.TempoBpm is < 50 or > 180)
        {
            throw new InvalidDataException("Tempo must be between 50 and 180 BPM.");
        }

        if (!MusicKey.TryParse(plan.Key, out _))
        {
            throw new InvalidDataException("Key must be a supported major or minor musical key.");
        }

        if (plan.Chords is null || plan.Chords.Count is < 1 or > 16 || plan.Chords.Any(chord => !Chord.TryParse(chord, out _)))
        {
            throw new InvalidDataException("The arrangement must contain 1 to 16 supported chord symbols.");
        }

        return plan;
    }

    [GeneratedRegex("^(?<root>[A-G](?:#|b)?)\\s+(?<mode>major|minor)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}

internal readonly record struct MusicKey(int RootPitchClass, bool IsMinor)
{
    private static readonly string[] Notes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly int[] MajorScale = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorScale = [0, 2, 3, 5, 7, 8, 10];

    public IReadOnlyList<int> Scale => IsMinor ? MinorScale : MajorScale;

    /// <summary>Convierte una tonalidad escrita, por ejemplo «A minor», a raíz y escala.</summary>
    public static bool TryParse(string? value, out MusicKey key)
    {
        key = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Regex.Match(value.Trim(), "^(?<root>[A-G](?:#|b)?)\\s+(?<mode>major|minor)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success || !TryParsePitchClass(match.Groups["root"].Value, out var pitchClass))
        {
            return false;
        }

        key = new MusicKey(pitchClass, string.Equals(match.Groups["mode"].Value, "minor", StringComparison.OrdinalIgnoreCase));
        return true;
    }

    /// <summary>Obtiene una nota MIDI de la escala, también para grados fuera de la primera octava.</summary>
    public int GetScaleNote(int degree, int octaveOffset)
    {
        var normalizedDegree = ((degree % 7) + 7) % 7;
        var scaleOctave = (int)Math.Floor(degree / 7d);
        return 60 + RootPitchClass + Scale[normalizedDegree] + ((scaleOctave + octaveOffset) * 12);
    }

    private static bool TryParsePitchClass(string note, out int pitchClass)
    {
        var letter = char.ToUpperInvariant(note[0]);
        pitchClass = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1
        };

        if (pitchClass < 0)
        {
            return false;
        }

        if (note.Length == 2)
        {
            pitchClass += note[1] == '#' ? 1 : note[1] == 'b' ? -1 : 0;
        }

        pitchClass = (pitchClass + 12) % 12;
        return true;
    }
}

internal readonly record struct Chord(int RootPitchClass, IReadOnlyList<int> Intervals)
{
    // Solo se aceptan estas calidades para que el compositor no interprete símbolos desconocidos.
    private static readonly Dictionary<string, int[]> Qualities = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = [0, 4, 7],
        ["maj"] = [0, 4, 7],
        ["m"] = [0, 3, 7],
        ["min"] = [0, 3, 7],
        ["dim"] = [0, 3, 6],
        ["aug"] = [0, 4, 8],
        ["sus2"] = [0, 2, 7],
        ["sus4"] = [0, 5, 7],
        ["7"] = [0, 4, 7, 10],
        ["maj7"] = [0, 4, 7, 11],
        ["m7"] = [0, 3, 7, 10],
        ["min7"] = [0, 3, 7, 10],
        ["m7b5"] = [0, 3, 6, 10],
        ["dim7"] = [0, 3, 6, 9],
        ["6"] = [0, 4, 7, 9],
        ["m6"] = [0, 3, 7, 9]
    };

    /// <summary>Interpreta la raíz y calidad de un acorde y devuelve sus intervalos en semitonos.</summary>
    public static bool TryParse(string? value, out Chord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var symbol = value.Trim();
        var rootLength = symbol.Length > 1 && symbol[1] is '#' or 'b' ? 2 : 1;
        if (!TryParseRoot(symbol[..rootLength], out var root))
        {
            return false;
        }

        var quality = symbol[rootLength..];
        if (!Qualities.TryGetValue(quality, out var intervals))
        {
            return false;
        }

        chord = new Chord(root, intervals);
        return true;
    }

    private static bool TryParseRoot(string value, out int pitchClass)
    {
        pitchClass = value[0] switch
        {
            'C' or 'c' => 0,
            'D' or 'd' => 2,
            'E' or 'e' => 4,
            'F' or 'f' => 5,
            'G' or 'g' => 7,
            'A' or 'a' => 9,
            'B' or 'b' => 11,
            _ => -1
        };
        if (pitchClass < 0)
        {
            return false;
        }

        if (value.Length == 2)
        {
            pitchClass += value[1] == '#' ? 1 : -1;
        }

        pitchClass = (pitchClass + 12) % 12;
        return true;
    }
}
