using MelodyFlow.Worker;
using MelodyFlow.Application;
using MelodyFlow.Infrastructure;
using MelodyFlow.Infrastructure.MusicGeneration;
using MelodyFlow.Infrastructure.VideoRendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment())
{
    // Launch-profile environment variables hold machine-local settings; appsettings.Local is not a source.
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
}
// El Worker usa la misma base que la API para reclamar trabajos y guardar sus estados.
var connectionString = builder.Configuration.GetConnectionString("MelodyFlow")
    ?? throw new InvalidOperationException(
        "Connection string 'MelodyFlow' is missing. Set it in the local launch profile or process environment.");

builder.Services.AddMelodyFlowApplication();
builder.Services.AddMelodyFlowInfrastructure(
    connectionString,
    builder.Configuration["Media:StorageRoot"],
    builder.Configuration["Media:TempRoot"]);
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

var videoRenderingOptions = builder.Configuration
    .GetSection("VideoRendering")
    .Get<VideoRenderingOptions>() ?? new VideoRenderingOptions();
var videoRenderingProblems = builder.Services.AddMelodyFlowVideoRendering(videoRenderingOptions);
if (videoRenderingProblems.Count == 0)
{
    builder.Services.AddScoped<IGenerationJobHandler, VideoRenderJobHandler>();
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
if (videoRenderingProblems.Count > 0)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("VideoRenderingConfiguration");
    logger.LogWarning(
        "Video render jobs are paused because FFmpeg is not configured: {ConfigurationProblems}",
        string.Join("; ", videoRenderingProblems));
}

host.Run();
