using MelodyFlow.Worker;
using MelodyFlow.Application;
using MelodyFlow.Infrastructure;
using MelodyFlow.Infrastructure.MusicGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    // Local overrides are ignored by Git; environment variables and CLI args keep higher precedence.
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
}
// El Worker usa la misma base que la API para reclamar trabajos y guardar sus estados.
var connectionString = builder.Configuration.GetConnectionString("MelodyFlow")
    ?? throw new InvalidOperationException(
        "Connection string 'MelodyFlow' is missing. Configure it with User Secrets or an environment variable.");

builder.Services.AddMelodyFlowApplication();
builder.Services.AddMelodyFlowInfrastructure(
    connectionString,
    builder.Configuration["Media:StorageRoot"]);
builder.Services.Configure<GenerationQueueOptions>(builder.Configuration.GetSection("Queue"));

var musicGenerationOptions = builder.Configuration
    .GetSection("MusicGeneration")
    .Get<MusicGenerationOptions>() ?? new MusicGenerationOptions();
var musicGenerationProblems = builder.Services.AddMelodyFlowMusicGeneration(musicGenerationOptions);
if (musicGenerationProblems.Count == 0)
{
    // Sin herramientas locales listas no se reclama trabajo musical que el proceso no pueda completar.
    builder.Services.AddScoped<IGenerationJobHandler, MusicGenerationJobHandler>();
}

builder.Services.AddHostedService<GenerationQueueWorker>();

var host = builder.Build();
if (musicGenerationProblems.Count > 0)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MusicGenerationConfiguration");
    logger.LogWarning(
        "Music jobs are paused because local generation is not configured: {ConfigurationProblems}",
        string.Join("; ", musicGenerationProblems));
}

host.Run();
