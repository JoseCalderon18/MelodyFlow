namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Selects a plan phrase by form so a whole song does not cycle the same four chords.</summary>
internal static class HarmonicPhrasePlanner
{
    private const int ChordsPerPhrase = 4;

    public static int GetChordIndex(int chordCount, ArrangementSection section, long bar)
    {
        if (chordCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chordCount));
        }

        var barsIntoSection = Math.Max(0, bar - section.StartBar);
        var phraseIndex = (int)(barsIntoSection / ChordsPerPhrase);
        var phrasePosition = (int)(barsIntoSection % ChordsPerPhrase);
        var blockCount = (chordCount + ChordsPerPhrase - 1) / ChordsPerPhrase;
        var preferredBlock = section.Type switch
        {
            ArrangementSectionType.Variation => 2 + (phraseIndex % 2),
            ArrangementSectionType.Break => 3,
            ArrangementSectionType.Theme or ArrangementSectionType.Return => phraseIndex % 2,
            _ => 0
        };

        var block = preferredBlock % blockCount;
        return ((block * ChordsPerPhrase) + phrasePosition) % chordCount;
    }
}
