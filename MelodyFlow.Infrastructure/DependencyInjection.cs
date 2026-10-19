using MelodyFlow.Application.Projects;
using MelodyFlow.Application.Jobs;
using MelodyFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MelodyFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMelodyFlowInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<MelodyFlowDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IMusicProjectRepository, MusicProjectRepository>();
        services.AddScoped<IGenerationJobRepository, GenerationJobRepository>();
        return services;
    }
}
