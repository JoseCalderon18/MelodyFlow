using MelodyFlow.WebApp.Components;
using MelodyFlow.WebApp.Services;

// La interfaz Blazor se configura como aplicación interactiva de servidor.
var builder = WebApplication.CreateBuilder(args);

// Registra los servicios de componentes Razor.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["MelodyFlowApi:BaseUrl"] ?? "http://localhost:5032/";
builder.Services.AddHttpClient<MelodyFlowApiClient>(client =>
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute));

var app = builder.Build();

// Configura el procesamiento HTTP de la interfaz.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HSTS indica al navegador que use HTTPS durante 30 días en futuras visitas.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
