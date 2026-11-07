using System.ComponentModel.DataAnnotations;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Contracts.V1.MusicTracks;
using MelodyFlow.Contracts.V1.Projects;
using MelodyFlow.WebApp.Services;
using Microsoft.AspNetCore.Components;

namespace MelodyFlow.WebApp.Components.Pages;

public partial class Home
{
    [Inject]
    private MelodyFlowApiClient Api { get; set; } = default!;

    private readonly MusicProjectForm form = new();
    private ProjectResponse? project;
    private GenerationJobResponse? job;
    private MusicTrackResponse? track;
    private bool submitting;
    private bool refreshing;
    private string? errorMessage;

    private string ShortJobId => job?.Id.ToString("N")[..8].ToUpperInvariant() ?? "";
    private string StatusClass => job?.Status?.ToLowerInvariant() ?? "";

    private string StatusLabel => job?.Status switch
    {
        "Queued" => "EN COLA",
        "Running" => "GENERANDO",
        "Retrying" => "REINTENTANDO",
        "Completed" => "COMPLETADO",
        "Failed" => "ERROR",
        "Cancelled" => "CANCELADO",
        _ => job?.Status?.ToUpperInvariant() ?? ""
    };

    private string StatusDescription => job?.Status switch
    {
        "Queued" => "Tu solicitud está esperando a que el Worker la procese.",
        "Running" => "El Worker está creando y renderizando la pista.",
        "Retrying" => "El Worker está reintentando el trabajo.",
        "Completed" => "La pista ya está lista para escuchar.",
        "Failed" => "El trabajo no pudo completarse.",
        _ => "Estado actualizado desde la API."
    };

    private async Task CreateAsync()
    {
        submitting = true;
        errorMessage = null;
        job = null;
        track = null;

        try
        {
            project ??= await Api.CreateProjectAsync(
                new CreateMusicProjectRequest
                {
                    Title = form.Title,
                    Genre = form.Genre,
                    Mood = string.IsNullOrWhiteSpace(form.Mood) ? null : form.Mood,
                    TargetDurationSeconds = form.TargetDurationSeconds
                },
                CancellationToken.None);

            job = await Api.CreateMusicJobAsync(project.Id, form.UseAI, CancellationToken.None);
        }
        catch (ApiResponseException ex)
        {
            errorMessage = $"La API respondió con HTTP {(int)ex.StatusCode}: {ex.Message}";
        }
        catch (HttpRequestException)
        {
            errorMessage = "No se pudo conectar con la API en http://localhost:5032. Comprueba que MelodyFlow.API esté arrancada.";
        }
        catch (InvalidDataException ex)
        {
            errorMessage = $"La API devolvió una respuesta inesperada: {ex.Message}";
        }
        finally
        {
            submitting = false;
        }
    }

    private async Task RefreshAsync()
    {
        if (job is null)
        {
            return;
        }

        refreshing = true;
        errorMessage = null;

        try
        {
            job = await Api.GetMusicJobAsync(job.Id, CancellationToken.None);
            if (job?.Status == "Completed")
            {
                track = await Api.GetTrackAsync(job.Id, CancellationToken.None);
            }
        }
        catch (ApiResponseException ex)
        {
            errorMessage = $"La API respondió con HTTP {(int)ex.StatusCode}: {ex.Message}";
        }
        catch (HttpRequestException)
        {
            errorMessage = "No se pudo consultar la API. Comprueba que siga arrancada.";
        }
        finally
        {
            refreshing = false;
        }
    }

    private sealed class MusicProjectForm
    {
        [Required(ErrorMessage = "Escribe un nombre para el proyecto.")]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un género.")]
        [StringLength(80, MinimumLength = 1)]
        public string Genre { get; set; } = string.Empty;

        [StringLength(80)]
        public string? Mood { get; set; }

        [Range(1, 4800)]
        public int TargetDurationSeconds { get; set; } = 60;

        public bool UseAI { get; set; }
    }
}
