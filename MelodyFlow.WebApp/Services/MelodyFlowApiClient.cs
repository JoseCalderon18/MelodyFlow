using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using MelodyFlow.Contracts.V1;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Contracts.V1.MusicTracks;
using MelodyFlow.Contracts.V1.Projects;
using MelodyFlow.Contracts.V1.RenderedVideos;
using MelodyFlow.Contracts.V1.VisualAssets;
using Microsoft.AspNetCore.Components.Forms;

namespace MelodyFlow.WebApp.Services;

/// <summary>Cliente server-side de Blazor para crear y consultar trabajos a través de la API pública.</summary>
public sealed class MelodyFlowApiClient(HttpClient httpClient)
{
    public async Task<ProjectResponse> CreateProjectAsync(
        CreateMusicProjectRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("api/v1/projects", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al crear el proyecto.");
    }

    public async Task<GenerationJobResponse> CreateMusicJobAsync(
        Guid projectId,
        bool useAI,
        string? preferredKey,
        string? preferredScale,
        CancellationToken cancellationToken)
    {
        var requestedParameters = new Dictionary<string, object?>
        {
            ["useAI"] = useAI
        };
        if (!string.IsNullOrWhiteSpace(preferredKey))
        {
            requestedParameters["preferredKey"] = preferredKey;
        }

        if (!string.IsNullOrWhiteSpace(preferredScale))
        {
            requestedParameters["preferredScale"] = preferredScale;
        }

        using var parameters = JsonDocument.Parse(JsonSerializer.Serialize(requestedParameters));
        var request = new CreateGenerationJobRequest
        {
            MusicProjectId = projectId,
            RequestedParameters = parameters.RootElement.Clone()
        };

        using var response = await httpClient.PostAsJsonAsync("api/v1/generation-jobs", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GenerationJobResponse>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al crear el trabajo.");
    }

    public async Task<GenerationJobResponse?> GetMusicJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/v1/generation-jobs/{jobId:D}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GenerationJobResponse>(cancellationToken);
    }

    public async Task<MusicTrackResponse?> GetTrackAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/v1/generation-jobs/{jobId:D}/track", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MusicTrackResponse>(cancellationToken);
    }

    public async Task<PagedResponse<MusicTrackResponse>> GetMusicTracksAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/v1/music-tracks?page={page}&pageSize={pageSize}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponse<MusicTrackResponse>>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al listar las canciones.");
    }

    public async Task<PagedResponse<ProjectResponse>> GetProjectsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/v1/projects?page={page}&pageSize={pageSize}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponse<ProjectResponse>>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al listar los proyectos.");
    }

    public async Task<PagedResponse<VisualAssetResponse>> GetVisualAssetsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/v1/visual-assets?page={page}&pageSize={pageSize}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponse<VisualAssetResponse>>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al listar los clips.");
    }

    public async Task<PagedResponse<RenderedVideoResponse>> GetRenderedVideosAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"api/v1/rendered-videos?page={page}&pageSize={pageSize}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResponse<RenderedVideoResponse>>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al listar los videoclips.");
    }

    public async Task<VisualAssetResponse> ImportVideoAsync(
        IBrowserFile file,
        string? providerName,
        CancellationToken cancellationToken)
    {
        using var multipart = new MultipartFormDataContent();
        await using var fileStream = file.OpenReadStream(512L * 1024 * 1024, cancellationToken);
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        multipart.Add(streamContent, "File", file.Name);
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            multipart.Add(new StringContent(providerName), "ProviderName");
        }

        using var response = await httpClient.PostAsync("api/v1/visual-assets/import", multipart, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<VisualAssetResponse>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al importar el clip.");
    }

    public async Task<GenerationJobResponse> CreateRenderedVideoAsync(
        Guid projectId,
        Guid trackId,
        Guid visualAssetId,
        string title,
        CancellationToken cancellationToken)
    {
        var request = new CreateRenderedVideoRequest
        {
            MusicProjectId = projectId,
            MusicTrackId = trackId,
            VisualAssetId = visualAssetId,
            Title = title
        };
        using var response = await httpClient.PostAsJsonAsync("api/v1/rendered-videos", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<GenerationJobResponse>(cancellationToken)
            ?? throw new InvalidDataException("La API devolvió una respuesta vacía al encolar el videoclip.");
    }

    public async Task<RenderedVideoResponse?> GetRenderedVideoByJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"api/v1/rendered-videos/jobs/{jobId:D}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<RenderedVideoResponse>(cancellationToken);
    }

    public Uri GetAudioUri(string relativeUrl) => new(httpClient.BaseAddress!, relativeUrl);

    public Uri GetVideoUri(string relativeUrl) => new(httpClient.BaseAddress!, relativeUrl);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = await ReadProblemDetailAsync(response, cancellationToken);
        throw new ApiResponseException(response.StatusCode, detail);
    }

    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var title = GetString(root, "title");
            var detail = GetString(root, "detail");
            var errors = new List<string>();
            if (root.TryGetProperty("errors", out var errorObject) && errorObject.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in errorObject.EnumerateObject())
                {
                    var messages = property.Value.ValueKind == JsonValueKind.Array
                        ? property.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString())
                        : Enumerable.Empty<string?>();
                    errors.AddRange(messages.Where(message => !string.IsNullOrWhiteSpace(message)).Select(message => $"{property.Name}: {message}"));
                }
            }

            var parts = new[] { title, detail }.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
            parts.AddRange(errors);
            return parts.Count > 0 ? string.Join(" ", parts) : "La API rechazó la solicitud sin detallar el motivo.";
        }
        catch (JsonException)
        {
            return "La API devolvió un error sin un detalle legible.";
        }
        catch (NotSupportedException)
        {
            return "La API devolvió un error sin un detalle legible.";
        }
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed class ApiResponseException(HttpStatusCode statusCode, string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
