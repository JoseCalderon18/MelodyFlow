namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Per-stem gain and pan controls, applied after FluidSynth renders each part.</summary>
internal sealed record StemMixSettings
{
    public double ChordsGainDb { get; init; }
    public double BassGainDb { get; init; }
    public double LeadGainDb { get; init; }
    public double DrumsGainDb { get; init; }
    public double ChordsPan { get; init; }
    public double BassPan { get; init; }
    public double LeadPan { get; init; }
    public double DrumsPan { get; init; }

    public StemMixSettings Validate()
    {
        ValidateGain(ChordsGainDb, nameof(ChordsGainDb));
        ValidateGain(BassGainDb, nameof(BassGainDb));
        ValidateGain(LeadGainDb, nameof(LeadGainDb));
        ValidateGain(DrumsGainDb, nameof(DrumsGainDb));
        ValidatePan(ChordsPan, nameof(ChordsPan));
        ValidatePan(BassPan, nameof(BassPan));
        ValidatePan(LeadPan, nameof(LeadPan));
        ValidatePan(DrumsPan, nameof(DrumsPan));
        return this;
    }

    public double GainFor(string role) => role switch
    {
        "chords" => ChordsGainDb,
        "bass" => BassGainDb,
        "lead" => LeadGainDb,
        "drums" => DrumsGainDb,
        _ => throw new InvalidDataException("The requested audio stem is not supported.")
    };

    public double PanFor(string role) => role switch
    {
        "chords" => ChordsPan,
        "bass" => BassPan,
        "lead" => LeadPan,
        "drums" => DrumsPan,
        _ => throw new InvalidDataException("The requested audio stem is not supported.")
    };

    private static void ValidateGain(double value, string name)
    {
        if (!double.IsFinite(value) || value is < -18 or > 6)
        {
            throw new InvalidDataException($"Mixer gain '{name}' must be between -18 and +6 dB.");
        }
    }

    private static void ValidatePan(double value, string name)
    {
        if (!double.IsFinite(value) || value is < -1 or > 1)
        {
            throw new InvalidDataException($"Mixer pan '{name}' must be between -1 and +1.");
        }
    }
}
