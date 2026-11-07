using MelodyFlow.Application.Music;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Registra los adaptadores locales que componen el flujo de generación musical.</summary>
public static class MelodyFlowMusicGenerationServices
{
    /// <summary>
    /// Registra el flujo musical solo cuando están configuradas todas las herramientas y la licencia.
    /// </summary>
    public static IReadOnlyList<string> AddMelodyFlowMusicGeneration(
        this IServiceCollection services,
        MusicGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton<IOptions<MusicGenerationOptions>>(Options.Create(options));

        // Si falta una herramienta o evidencia de licencia, no se ofrece el pipeline al Worker.
        var configurationProblems = options.GetConfigurationProblems();
        if (configurationProblems.Count > 0)
        {
            return configurationProblems;
        }

        services.AddHttpClient<IAiArrangementPlanner, OpenAiCompatibleArrangementPlanner>(client =>
        {
            if (options.Planner.TimeoutSeconds is >= 30 and <= 3_600)
            {
                client.Timeout = TimeSpan.FromSeconds(options.Planner.TimeoutSeconds);
            }
        });
        services.AddScoped<IArrangementPlanProvider, ArrangementPlanProvider>();
        services.AddScoped<IMidiComposer, MidiComposer>();
        services.AddScoped<LocalAudioRenderer>();
        services.AddScoped<IMusicGenerationPipeline, MusicGenerationPipeline>();
        return [];
    }
}
