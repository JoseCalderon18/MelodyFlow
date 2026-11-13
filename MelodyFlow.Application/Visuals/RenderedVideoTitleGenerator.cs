using System.Globalization;
using System.Text;
using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Visuals;

/// <summary>Creates a readable default video title from its song title and project style.</summary>
internal static class RenderedVideoTitleGenerator
{
    private static readonly IReadOnlyDictionary<string, string> MoodLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["calm"] = "Relaxing",
        ["relaxing"] = "Relaxing",
        ["focused"] = "Focus",
        ["energetic"] = "Energetic",
        ["dark"] = "Dark",
        ["uplifting"] = "Uplifting",
        ["romantic"] = "Romantic",
        ["epic"] = "Epic",
        ["dreamy"] = "Dreamy"
    };

    private static readonly IReadOnlyDictionary<string, string> GenreLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["lofi"] = "Lo-fi",
        ["lo-fi"] = "Lo-fi",
        ["r&b"] = "R&B",
        ["hip-hop"] = "Hip-Hop",
        ["drum and bass"] = "Drum and Bass",
        ["dembow"] = "Dembow",
        ["bossa nova"] = "Bossa Nova",
        ["synthwave"] = "Synthwave"
    };

    public static string Generate(string songTitle, string genre, string? mood)
    {
        var cleanTitle = Normalize(songTitle);
        var cleanGenre = DisplayLabel(genre, GenreLabels);
        var cleanMood = string.IsNullOrWhiteSpace(mood) ? null : DisplayLabel(mood, MoodLabels);
        var descriptor = string.IsNullOrWhiteSpace(cleanMood) ||
            string.Equals(cleanMood, cleanGenre, StringComparison.OrdinalIgnoreCase)
                ? cleanGenre
                : $"{cleanMood} {cleanGenre}";
        var suffix = $"{descriptor} Music";
        var availableTitleLength = RenderedVideo.MaxTitleLength - suffix.Length - 3;

        if (availableTitleLength <= 0)
        {
            return suffix[..Math.Min(suffix.Length, RenderedVideo.MaxTitleLength)];
        }

        if (cleanTitle.Length > availableTitleLength)
        {
            cleanTitle = cleanTitle[..availableTitleLength].TrimEnd();
        }

        return $"{cleanTitle} | {suffix}";
    }

    private static string DisplayLabel(string value, IReadOnlyDictionary<string, string> knownLabels)
    {
        var clean = Normalize(value);
        if (knownLabels.TryGetValue(clean, out var label))
        {
            return label;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean.ToLowerInvariant());
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character == '|' ? '—' : character);
        }

        return builder.ToString();
    }
}
