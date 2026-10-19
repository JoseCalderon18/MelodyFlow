using MelodyFlow.Worker;
using MelodyFlow.Application;
using MelodyFlow.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("MelodyFlow")
    ?? throw new InvalidOperationException(
        "Connection string 'MelodyFlow' is missing. Configure it with User Secrets or an environment variable.");

builder.Services.AddMelodyFlowApplication();
builder.Services.AddMelodyFlowInfrastructure(connectionString);
builder.Services.Configure<GenerationQueueOptions>(builder.Configuration.GetSection("Queue"));
builder.Services.AddHostedService<GenerationQueueWorker>();

var host = builder.Build();
host.Run();
