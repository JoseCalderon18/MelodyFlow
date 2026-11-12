using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Infrastructure.VideoRendering;

/// <summary>Registra FFmpeg para el Worker únicamente cuando sus herramientas están disponibles.</summary>
public static class MelodyFlowVideoRenderingServices
{
    public static IReadOnlyList<string> AddMelodyFlowVideoRendering(
        this IServiceCollection services,
        VideoRenderingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton<IOptions<VideoRenderingOptions>>(Options.Create(options));
        var problems = options.GetConfigurationProblems();
        if (problems.Count == 0)
        {
            services.AddScoped<LocalVideoRenderer>();
        }

        return problems;
    }
}
