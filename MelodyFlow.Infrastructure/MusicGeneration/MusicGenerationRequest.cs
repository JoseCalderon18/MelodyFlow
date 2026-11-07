using System.Text.Json;
using MelodyFlow.Application.Music;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Preferencias validadas recibidas con un trabajo de música.</summary>
internal sealed record MusicGenerationRequest(
    bool UseAI,
    int? VariationSeed,
    int? PreferredTempoBpm,
    string? PreferredKey)
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
                        MusicKey.TryParse(property.Value.GetString(), out _):
                        key = property.Value.GetString();
                        break;
                    default:
                        throw new JsonException("A music parameter was invalid or not recognized.");
                }
            }

            return new MusicGenerationRequest(useAI, seed, tempo, key);
        }
        catch (JsonException exception)
        {
            throw new MusicGenerationException(
                "unsupported_music_preferences",
                "Music job parameters may contain useAI (boolean), variationSeed, preferredTempoBpm, and preferredKey.",
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
