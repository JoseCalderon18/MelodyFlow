using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MelodyFlow.Application.Music;
using MelodyFlow.Domain.Entities;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>
/// Llama al endpoint local compatible con OpenAI de LM Studio y acepta solo planes JSON validados.
/// </summary>
internal sealed class OpenAiCompatibleArrangementPlanner(
    HttpClient httpClient,
    IOptions<MusicGenerationOptions> options) : IAiArrangementPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ArrangementPlan> CreatePlanAsync(
        MusicProject project,
        MusicGenerationRequest requestOptions,
        int seed,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var plannerProblems = settings.GetAiPlannerConfigurationProblems();
        if (plannerProblems.Count > 0)
        {
            throw new MusicGenerationException(
                "ai_planner_not_configured",
                "AI-assisted mode requires a valid local LM Studio endpoint and a loaded model.",
                isTransient: false);
        }

        var endpoint = BuildEndpoint(settings.Planner.BaseUrl);
        // El modelo devuelve instrucciones estructuradas; la síntesis ocurre en C# y herramientas locales.
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = JsonContent.Create(new
        {
            model = settings.Planner.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a harmony and arrangement planner. Return one JSON object only. Follow this exact schema: " +
                        "{\"schemaVersion\":1,\"tempoBpm\":96,\"key\":\"A minor\",\"scale\":\"dorian\",\"groove\":\"lofi\",\"instrumentSet\":\"lofi\",\"form\":\"aaba\",\"textureDensity\":\"balanced\",\"mixProfile\":\"warm\",\"paletteVariant\":1,\"chords\":[\"Am7\",\"Dm9\",\"G7\",\"Em7\"],\"variationSeed\":123}. " +
                        "Use a tempo from 50 to 180 BPM, a supported major/minor tonal center, and 4 to 16 chord symbols. " +
                        "Supported seven-note scales: ionian, dorian, phrygian, lydian, mixolydian, aeolian, locrian, harmonic-minor, melodic-minor. " +
                        "Supported grooves: ambient, lofi, backbeat, four-on-floor, cinematic, pulse, arpeggiated, shuffle, swing, broken-beat, clave. " +
                        "Supported forms: evolving-arc, aaba, build-break-return, cinematic-arc, steady-groove. " +
                        "Supported textureDensity: sparse, balanced, layered. Supported mixProfile: intimate, balanced, spacious, warm. paletteVariant must be 0, 1, or 2. " +
                        "Supported instrumentSet values: ambient, lofi, pop, electronic, cinematic, rock, acoustic. " +
                        "Choose a form, groove, chord progression, density, mix and palette that fit the supplied genre and mood. Use connected voice leading, clear phrase endings, chord tones on strong melody beats, and avoid reusing the same pattern throughout the whole track. " +
                        "Supported chord suffixes are empty, maj, m, min, dim, aug, sus2, sus4, 7, maj7, m7, min7, m7b5, dim7, 6, m6, maj9, m9, 9, mMaj7."
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new
                    {
                        project.Title,
                        project.Genre,
                        project.Mood,
                        targetDurationSeconds = project.TargetDurationSeconds,
                        variationSeed = seed,
                        preferredTempoBpm = requestOptions.PreferredTempoBpm,
                        preferredKey = requestOptions.PreferredKey,
                        preferredScale = requestOptions.PreferredScale,
                        preferredGroove = requestOptions.PreferredGroove,
                        preferredForm = requestOptions.PreferredForm,
                        textureDensity = requestOptions.TextureDensity,
                        mixProfile = requestOptions.MixProfile
                    }, JsonOptions)
                }
            },
            temperature = 0.55,
            max_tokens = 1_200,
            stream = false,
            seed,
            response_format = new { type = "json_object" }
        }, options: JsonOptions);

        if (!string.IsNullOrWhiteSpace(settings.Planner.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Planner.ApiKey);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MusicGenerationException(
                "planner_timeout",
                "The local arrangement planner did not respond before its timeout.",
                isTransient: true);
        }
        catch (HttpRequestException exception)
        {
            throw new MusicGenerationException(
                "planner_unavailable",
                "The local arrangement planner is unavailable. Start LM Studio and load the configured model.",
                isTransient: true,
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var transient = response.StatusCode == HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500;
                throw new MusicGenerationException(
                    transient ? "planner_temporarily_unavailable" : "planner_request_rejected",
                    transient
                        ? "The arrangement planner is temporarily unavailable."
                        : "The arrangement planner rejected the request or model configuration.",
                    transient);
            }

            try
            {
                using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                var content = document.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new JsonException("The completion did not include message content.");
                }

                // La respuesta externa se valida otra vez antes de llegar al compositor MIDI.
                var plan = ArrangementPlanParser.Parse(content);
                return ApplyUserPreferences(plan, requestOptions, seed);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException)
            {
                throw new MusicGenerationException(
                    "planner_invalid_plan",
                    "The planner response was not a valid supported arrangement. Review the configured model's JSON output support.",
                    isTransient: false,
                    exception);
            }
        }
    }

    private static Uri BuildEndpoint(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return new Uri(
            normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"{normalized}/chat/completions",
            UriKind.Absolute);
    }

    private static ArrangementPlan ApplyUserPreferences(
        ArrangementPlan plan,
        MusicGenerationRequest request,
        int seed)
    {
        _ = MusicKey.TryParse(plan.Key, out var originalKey);
        var root = originalKey.RootPitchClass;
        var requestedFullKey = default(MusicKey);
        var explicitFullKey = request.PreferredKey is not null &&
            MusicKey.TryParse(request.PreferredKey, out requestedFullKey);
        var requestedRoot = 0;
        var explicitRoot = request.PreferredKey is not null &&
            MusicKey.TryParseRoot(request.PreferredKey, out requestedRoot);
        if (explicitRoot)
        {
            root = requestedRoot;
        }

        var scaleName = request.PreferredScale ??
            (explicitFullKey
                ? MusicalScaleCatalog.DefaultFor(requestedFullKey.IsMinor)
                : plan.Scale);
        var scale = MusicalScaleCatalog.GetRequired(scaleName);
        var keyName = $"{MusicalScaleCatalog.PitchClassNames[root]} {(scale.MinorColor ? "minor" : "major")}";
        var shift = (root - originalKey.RootPitchClass + 12) % 12;
        var chords = shift == 0
            ? plan.Chords
            : plan.Chords.Select(symbol => Chord.TryParse(symbol, out var chord)
                ? chord.TransposeSymbol(shift)
                : throw new InvalidDataException("The arrangement contains an unsupported chord.")).ToArray();

        return plan with
        {
            VariationSeed = seed,
            UsedAI = true,
            Key = keyName,
            Chords = chords,
            Scale = scale.Name,
            Groove = request.PreferredGroove ?? plan.Groove,
            Form = request.PreferredForm ?? plan.Form,
            TextureDensity = request.TextureDensity ?? plan.TextureDensity,
            MixProfile = request.MixProfile ?? plan.MixProfile,
            PaletteVariant = (plan.PaletteVariant + (int)((unchecked((uint)seed) >> 8) % 3)) % 3
        };
    }

}
