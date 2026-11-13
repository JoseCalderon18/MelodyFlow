using System.Text.Json;
using MelodyFlow.Application.Music;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Preferencias validadas recibidas con un trabajo de música.</summary>
internal sealed record MusicGenerationRequest(
    bool UseAI,
    int? VariationSeed,
    int? PreferredTempoBpm,
    string? PreferredKey,
    string? PreferredScale,
    string? PreferredGroove = null,
    string? PreferredForm = null,
    string? TextureDensity = null,
    string? MixProfile = null,
    StemMixSettings? Mixer = null)
{
    public static MusicGenerationRequest Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Music parameters must be an object.");
            }

            var useAI = false;
            int? seed = null;
            int? tempo = null;
            string? key = null;
            string? scale = null;
            string? groove = null;
            string? form = null;
            string? density = null;
            string? mixProfile = null;
            StemMixSettings? mixer = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "useAI" when property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                        useAI = property.Value.GetBoolean();
                        break;
                    case "variationSeed" when property.Value.TryGetInt32(out var parsedSeed):
                        seed = parsedSeed;
                        break;
                    case "preferredTempoBpm" when property.Value.TryGetInt32(out var parsedTempo) && parsedTempo is >= 50 and <= 180:
                        tempo = parsedTempo;
                        break;
                    case "preferredKey" when property.Value.ValueKind == JsonValueKind.String &&
                        (MusicKey.TryParse(property.Value.GetString(), out _) || MusicKey.TryParseRoot(property.Value.GetString()!, out _)):
                        key = property.Value.GetString();
                        break;
                    case "preferredScale" when property.Value.ValueKind == JsonValueKind.String &&
                        MusicalScaleCatalog.TryGet(property.Value.GetString(), out _):
                        scale = property.Value.GetString();
                        break;
                    case "preferredGroove" when property.Value.ValueKind == JsonValueKind.String &&
                        ArrangementStyleCatalog.IsSupportedGroove(property.Value.GetString()!):
                        groove = property.Value.GetString();
                        break;
                    case "preferredForm" when property.Value.ValueKind == JsonValueKind.String &&
                        ArrangementStyleCatalog.IsSupportedForm(property.Value.GetString()!):
                        form = property.Value.GetString();
                        break;
                    case "textureDensity" when property.Value.ValueKind == JsonValueKind.String &&
                        ArrangementStyleCatalog.IsSupportedDensity(property.Value.GetString()!):
                        density = property.Value.GetString();
                        break;
                    case "mixProfile" when property.Value.ValueKind == JsonValueKind.String &&
                        ArrangementStyleCatalog.IsSupportedMixProfile(property.Value.GetString()!):
                        mixProfile = property.Value.GetString();
                        break;
                    case "mixer" when property.Value.ValueKind == JsonValueKind.Object:
                        mixer = ParseMixer(property.Value);
                        break;
                    default:
                        throw new JsonException("A music parameter was invalid or not recognized.");
                }
            }

            return new MusicGenerationRequest(useAI, seed, tempo, key, scale, groove, form, density, mixProfile, mixer);
        }
        catch (JsonException exception)
        {
            throw new MusicGenerationException(
                "unsupported_music_preferences",
                "Music job parameters contain an invalid value. Supported options include AI, tempo, key, scale, groove, form, density, mix profile, and per-part mixer controls.",
                isTransient: false,
                exception);
        }
    }

    private static StemMixSettings ParseMixer(JsonElement value)
    {
        var mixer = new StemMixSettings();
        foreach (var property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out var number))
            {
                throw new JsonException("Mixer controls must be finite numbers.");
            }

            mixer = property.Name switch
            {
                "chordsGainDb" => mixer with { ChordsGainDb = number },
                "bassGainDb" => mixer with { BassGainDb = number },
                "leadGainDb" => mixer with { LeadGainDb = number },
                "drumsGainDb" => mixer with { DrumsGainDb = number },
                "chordsPan" => mixer with { ChordsPan = number },
                "bassPan" => mixer with { BassPan = number },
                "leadPan" => mixer with { LeadPan = number },
                "drumsPan" => mixer with { DrumsPan = number },
                _ => throw new JsonException("A mixer control was not recognized.")
            };
        }

        try
        {
            return mixer.Validate();
        }
        catch (InvalidDataException exception)
        {
            throw new JsonException("A mixer control was outside its supported range.", exception);
        }
    }

    public int GetVariationSeed(Guid generationJobId)
    {
        if (VariationSeed is int seed)
        {
            return seed;
        }

        var jobSeed = BitConverter.ToInt32(generationJobId.ToByteArray(), 0) & int.MaxValue;
        return jobSeed == 0 ? 1 : jobSeed;
    }
}
