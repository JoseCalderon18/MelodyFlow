using MelodyFlow.Application.Projects;
using MelodyFlow.Application.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace MelodyFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMelodyFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<MusicProjectApplicationService>();
        services.AddScoped<GenerationJobApplicationService>();
        services.AddSingleton<GenerationJobRetryPolicy>();
        return services;
    }
}
