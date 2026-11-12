using MelodyFlow.Application.Projects;
using MelodyFlow.Application.Jobs;
using MelodyFlow.Application.Music;
using MelodyFlow.Application.Visuals;
using Microsoft.Extensions.DependencyInjection;

namespace MelodyFlow.Application;

/// <summary>
/// Agrupa el registro de los casos de uso y políticas de la capa Application.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Añade los servicios de aplicación que usan tanto la API como el Worker.
    /// </summary>
    public static IServiceCollection AddMelodyFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<MusicProjectApplicationService>();
        services.AddScoped<GenerationJobApplicationService>();
        services.AddScoped<MusicTrackApplicationService>();
        services.AddScoped<VisualAssetApplicationService>();
        services.AddScoped<RenderedVideoApplicationService>();
        services.AddSingleton<GenerationJobRetryPolicy>();
        return services;
    }
}
