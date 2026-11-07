using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using MelodyFlow.Application;
using MelodyFlow.Infrastructure;
using MelodyFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    // Local overrides are ignored by Git; environment variables and CLI args keep higher precedence.
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);
}
// La API es el host público y registra Application e Infrastructure en este mismo proceso.
var connectionString = builder.Configuration.GetConnectionString("MelodyFlow")
    ?? throw new InvalidOperationException(
        "Connection string 'MelodyFlow' is missing. Configure it with User Secrets or an environment variable.");
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddMelodyFlowApplication();
builder.Services.AddMelodyFlowInfrastructure(
    connectionString,
    builder.Configuration["Media:StorageRoot"]);
builder.Services.AddCors(options => options.AddPolicy("LocalWebApp", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
// Esta fase local no usa login; el tráfico se limita por IP y los orígenes de navegador son configurables.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        await Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests",
            detail: "Wait before sending another request.")
            .ExecuteAsync(context.HttpContext);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
// CORS controla llamadas de navegador; n8n local llama a la API por HTTP de servidor a servidor.
app.UseCors("LocalWebApp");
app.UseRateLimiter();
app.MapGet("/api/health", async (
    MelodyFlowDbContext dbContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    // La respuesta informa disponibilidad sin devolver la conexión ni detalles internos de la excepción.
    try
    {
        if (await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return Results.Ok(new { status = "healthy", database = "reachable" });
        }

        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database unavailable",
            detail: "The application cannot reach its database.");
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        logger.LogWarning("Database connectivity check failed ({ExceptionType}).", exception.GetType().Name);
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database unavailable",
            detail: "The application cannot reach its database.");
    }
}).WithName("GetApiHealth");
app.MapControllers();

app.Run();

public partial class Program;
