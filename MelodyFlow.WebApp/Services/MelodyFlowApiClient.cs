using System.Net.Http.Json;
using System.Text.Json;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Contracts.V1.MusicTracks;
using MelodyFlow.Contracts.V1.Projects;

namespace MelodyFlow.WebApp.Services;

/// <summary>Cliente server-side de Blazor para crear y consultar trabajos a través de la API pública.</summary>
public sealed class MelodyFlowApiClient(HttpClient httpClient)
{
    public async Task<ProjectResponse> CreateProjectAsync(
        CreateMusicProjectRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/projects",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new InvalidDataException("The API returned an empty project response.");
    }

    public async Task<GenerationJobResponse> CreateMusicJobAsync(
        Guid projectId,
        bool useAI,
        CancellationToken cancellationToken)
    {
        using var parameters = JsonDocument.Parse(JsonSerializer.Serialize(new { useAI }));
        var request = new CreateGenerationJobRequest
        {
            MusicProjectId = projectId,
            RequestedParameters = parameters.RootElement.Clone()
        };

        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/generation-jobs",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GenerationJobResponse>(cancellationToken)
            ?? throw new InvalidDataException("The API returned an empty generation-job response.");
    }

    public Task<GenerationJobResponse?> GetMusicJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<GenerationJobResponse>(
            $"api/v1/generation-jobs/{jobId:D}",
            cancellationToken);

    public Task<MusicTrackResponse?> GetTrackAsync(Guid jobId, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<MusicTrackResponse>(
            $"api/v1/generation-jobs/{jobId:D}/track",
            cancellationToken);

    public Uri GetAudioUri(string relativeUrl) => new(httpClient.BaseAddress!, relativeUrl);
}
