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
    string? MixProfile = null)
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
                    default:
                        throw new JsonException("A music parameter was invalid or not recognized.");
                }
            }

            return new MusicGenerationRequest(useAI, seed, tempo, key, scale, groove, form, density, mixProfile);
        }
        catch (JsonException exception)
        {
            throw new MusicGenerationException(
                "unsupported_music_preferences",
                "Music job parameters contain an invalid value. Supported options include AI, tempo, key, scale, groove, form, density, and mix profile.",
                isTransient: false,
                exception);
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
