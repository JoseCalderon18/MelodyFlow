using System.ComponentModel.DataAnnotations;
using MelodyFlow.Contracts.V1.GenerationJobs;
using MelodyFlow.Contracts.V1.MusicTracks;
using MelodyFlow.Contracts.V1.Projects;
using MelodyFlow.Contracts.V1.RenderedVideos;
using MelodyFlow.Contracts.V1.VisualAssets;
using Microsoft.AspNetCore.Components.Forms;
using MelodyFlow.WebApp.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MelodyFlow.WebApp.Components.Pages;

public partial class Home : IAsyncDisposable
{
    private const int TrackPageSize = 6;
    private const int ProjectPageSize = 5;
    private static readonly TimeSpan JobPollInterval = TimeSpan.FromSeconds(2);

    [Inject]
    private MelodyFlowApiClient Api { get; set; } = default!;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    private ILogger<Home> Logger { get; set; } = default!;

    private readonly MusicProjectForm form = new();
    private readonly List<MusicTrackResponse> tracks = [];
    private readonly List<VisualAssetResponse> visualAssets = [];
    private readonly List<RenderedVideoResponse> renderedVideos = [];
    private readonly CancellationTokenSource componentLifetime = new();
    private List<ProjectResponse> projects = [];
    private ProjectResponse? project;
    private GenerationJobResponse? job;
    private MusicTrackResponse? track;
    private RenderedVideoResponse? renderedVideo;
    private Guid? selectedMusicTrackId;
    private Guid? selectedVisualAssetId;
    private string videoTitle = string.Empty;
    private string providerName = "Google Flow";
    private int trackCount;
    private int projectCount;
    private int visualAssetCount;
    private int renderedVideoCount;
    private int trackPage = 1;
    private int projectPage = 1;
    private bool loadingDashboard = true;
    private bool loadingTracks;
    private bool loadingProjects;
    private bool loadingMedia;
    private bool uploadingClip;
    private bool renderingVideo;
    private bool submitting;
    private bool refreshing;
    private Guid? deletingMediaId;
    private string? loadError;
    private string? errorMessage;
    private string? mediaError;
    private Task? jobPollingTask;

    private string ShortJobId => job?.Id.ToString("N")[..8].ToUpperInvariant() ?? string.Empty;
    private string StatusClass => job?.Status?.ToLowerInvariant() ?? string.Empty;

    private string StatusLabel => job?.Status switch
    {
        "Queued" => "EN COLA",
        "Running" => "GENERANDO",
        "Retrying" => "REINTENTANDO",
        "Completed" => "COMPLETADO",
        "Failed" => "ERROR",
        "Cancelled" => "CANCELADO",
        _ => job?.Status?.ToUpperInvariant() ?? string.Empty
    };

    private string StatusDescription => job?.Status switch
    {
        "Queued" => "Tu solicitud está esperando a que el Worker la procese.",
        "Running" => job.JobType == "Render"
            ? "FFmpeg está montando el vídeo; el porcentaje se actualiza mientras procesa."
            : "El Worker está componiendo la música y renderizando el audio.",
        "Retrying" => "El Worker está reintentando el trabajo.",
        "Completed" => job.JobType == "Render" ? "El videoclip ya está listo para reproducir." : "La pista ya está lista para escuchar.",
        "Failed" => "El trabajo no pudo completarse.",
        _ => "Estado actualizado desde la API."
    };

    protected override Task OnInitializedAsync() => LoadDashboardAsync();

    private async Task LoadDashboardAsync()
    {
        loadingDashboard = true;
        loadingTracks = true;
        loadError = null;

        try
        {
            var trackRequest = Api.GetMusicTracksAsync(1, TrackPageSize, CancellationToken.None);
            var projectRequest = Api.GetProjectsAsync(1, ProjectPageSize, CancellationToken.None);
            await Task.WhenAll(trackRequest, projectRequest);

            var trackPageResult = await trackRequest;
            tracks.Clear();
            tracks.AddRange(trackPageResult.Items);
            trackCount = trackPageResult.TotalCount;
            trackPage = 1;
            if (selectedMusicTrackId is null && tracks.Count > 0)
            {
                selectedMusicTrackId = tracks[0].Id;
            }
            var projectPageResult = await projectRequest;
            projects = projectPageResult.Items.ToList();
            projectCount = projectPageResult.TotalCount;
            projectPage = 1;

            await LoadVisualLibraryAsync();
        }
        catch (ApiResponseException exception)
        {
            loadError = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            loadError = "No se pudo conectar con la API. Comprueba que MelodyFlow.API esté iniciada.";
        }
        catch (InvalidDataException exception)
        {
            loadError = exception.Message;
        }
        finally
        {
            loadingDashboard = false;
            loadingTracks = false;
        }
    }

    private async Task LoadVisualLibraryAsync()
    {
        loadingMedia = true;
        mediaError = null;
        try
        {
            var assetRequest = Api.GetVisualAssetsAsync(1, 50, CancellationToken.None);
            var videoRequest = Api.GetRenderedVideosAsync(1, 50, CancellationToken.None);
            await Task.WhenAll(assetRequest, videoRequest);
            var assetPage = await assetRequest;
            var videoPage = await videoRequest;
            visualAssets.Clear();
            visualAssets.AddRange(assetPage.Items);
            visualAssetCount = assetPage.TotalCount;
            renderedVideos.Clear();
            renderedVideos.AddRange(videoPage.Items);
            renderedVideoCount = videoPage.TotalCount;
            if (selectedVisualAssetId is null && visualAssets.Count > 0)
            {
                selectedVisualAssetId = visualAssets[0].Id;
            }
        }
        catch (ApiResponseException exception) when (exception.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            mediaError = "La API aún no encuentra las tablas de clips y videoclips. Aplica la migración pendiente para activar esta sección.";
        }
        catch (ApiResponseException exception)
        {
            mediaError = $"La API respondió con HTTP {(int)exception.StatusCode} al cargar los clips.";
        }
        catch (HttpRequestException)
        {
            mediaError = "No se pudieron cargar los clips. Comprueba que la API siga iniciada.";
        }
        finally
        {
            loadingMedia = false;
        }
    }

    private async Task LoadMoreProjectsAsync()
    {
        if (loadingProjects || projects.Count >= projectCount)
        {
            return;
        }

        loadingProjects = true;
        loadError = null;
        try
        {
            var nextPage = await Api.GetProjectsAsync(projectPage + 1, ProjectPageSize, CancellationToken.None);
            projects.AddRange(nextPage.Items);
            projectPage = nextPage.Page;
            projectCount = nextPage.TotalCount;
        }
        catch (ApiResponseException exception)
        {
            loadError = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            loadError = "No se pudieron cargar más proyectos. Comprueba que la API siga iniciada.";
        }
        finally
        {
            loadingProjects = false;
        }
    }

    private async Task LoadMoreTracksAsync()
    {
        if (loadingTracks || tracks.Count >= trackCount)
        {
            return;
        }

        loadingTracks = true;
        loadError = null;
        try
        {
            var nextPage = await Api.GetMusicTracksAsync(trackPage + 1, TrackPageSize, CancellationToken.None);
            tracks.AddRange(nextPage.Items);
            trackPage = nextPage.Page;
            trackCount = nextPage.TotalCount;
        }
        catch (ApiResponseException exception)
        {
            loadError = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            loadError = "No se pudo cargar más música. Comprueba que la API siga iniciada.";
        }
        finally
        {
            loadingTracks = false;
        }
    }

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

            job = await Api.CreateMusicJobAsync(
                project.Id,
                form.UseAI,
                string.IsNullOrWhiteSpace(form.PreferredKey) ? null : form.PreferredKey,
                string.IsNullOrWhiteSpace(form.PreferredScale) ? null : form.PreferredScale,
                string.IsNullOrWhiteSpace(form.PreferredGroove) ? null : form.PreferredGroove,
                string.IsNullOrWhiteSpace(form.PreferredForm) ? null : form.PreferredForm,
                string.IsNullOrWhiteSpace(form.TextureDensity) ? null : form.TextureDensity,
                string.IsNullOrWhiteSpace(form.MixProfile) ? null : form.MixProfile,
                form.ChordsGainDb,
                form.BassGainDb,
                form.LeadGainDb,
                form.DrumsGainDb,
                form.ChordsPan / 50d,
                form.BassPan / 50d,
                form.LeadPan / 50d,
                form.DrumsPan / 50d,
                CancellationToken.None);
            project = null;
            renderedVideo = null;
            ResetForm();
            await LoadDashboardAsync();
            StartProgressPolling(job.Id);
        }
        catch (ApiResponseException exception)
        {
            errorMessage = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            errorMessage = "No se pudo conectar con la API en http://localhost:5032. Comprueba que MelodyFlow.API esté iniciada.";
        }
        catch (InvalidDataException exception)
        {
            errorMessage = $"La API devolvió una respuesta inesperada: {exception.Message}";
        }
        finally
        {
            submitting = false;
        }
    }

    private async Task RefreshAsync()
        => await RefreshAsync(CancellationToken.None);

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (job is null)
        {
            return;
        }

        refreshing = true;
        errorMessage = null;

        try
        {
            job = await Api.GetMusicJobAsync(job.Id, cancellationToken);
            if (job?.Status == "Completed")
            {
                if (job.JobType == "Render")
                {
                    renderedVideo = await Api.GetRenderedVideoByJobAsync(job.Id, cancellationToken);
                }
                else
                {
                    track = await Api.GetTrackAsync(job.Id, cancellationToken);
                }

                await LoadDashboardAsync();
            }
        }
        catch (ApiResponseException exception)
        {
            errorMessage = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            errorMessage = "No se pudo consultar la API. Comprueba que siga iniciada.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally
        {
            refreshing = false;
        }
    }

    private async Task ImportClipAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file.Size <= 0 || file.Size > 512L * 1024 * 1024)
        {
            mediaError = "El clip debe pesar entre 1 byte y 512 MB.";
            return;
        }

        uploadingClip = true;
        mediaError = null;
        try
        {
            var asset = await Api.ImportVideoAsync(file, providerName, componentLifetime.Token);
            visualAssets.Insert(0, asset);
            visualAssetCount++;
            selectedVisualAssetId = asset.Id;
        }
        catch (ApiResponseException exception)
        {
            mediaError = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (TaskCanceledException exception)
        {
            Logger.LogWarning("MP4 upload timed out or was cancelled ({ExceptionType}); file size {FileSizeBytes} bytes.", exception.GetType().Name, file.Size);
            mediaError = "La subida tardó demasiado o se canceló. Prueba de nuevo; el límite está en 512 MB.";
        }
        catch (InvalidDataException exception)
        {
            mediaError = exception.Message;
        }
        catch (HttpRequestException exception)
        {
            Logger.LogWarning("MP4 upload could not reach the API ({ExceptionType}); file size {FileSizeBytes} bytes.", exception.GetType().Name, file.Size);
            mediaError = $"No se pudo completar la subida a la API. Comprueba que la API esté iniciada con su perfil HTTPS. Detalle: {exception.Message}";
        }
        catch (IOException exception)
        {
            Logger.LogWarning("MP4 upload failed while reading or sending the file ({ExceptionType}); file size {FileSizeBytes} bytes.", exception.GetType().Name, file.Size);
            mediaError = $"No se pudo leer o enviar el archivo local. Detalle: {exception.Message}";
        }
        finally
        {
            uploadingClip = false;
        }
    }

    private async Task DeleteTrackAsync(MusicTrackResponse item)
    {
        if (!await JavaScript.InvokeAsync<bool>("confirm", $"¿Eliminar «{item.Title}»? Se borrará también el MP3 del almacenamiento local."))
        {
            return;
        }

        deletingMediaId = item.Id;
        mediaError = null;
        try
        {
            var warning = await Api.DeleteMusicTrackAsync(item.Id, CancellationToken.None);
            tracks.Remove(item);
            trackCount = Math.Max(0, trackCount - 1);
            if (selectedMusicTrackId == item.Id)
            {
                selectedMusicTrackId = null;
            }

            if (track?.Id == item.Id)
            {
                track = null;
            }

            mediaError = warning;
        }
        catch (ApiResponseException exception)
        {
            mediaError = $"No se pudo eliminar la canción (HTTP {(int)exception.StatusCode}): {exception.Message}";
        }
        catch (HttpRequestException)
        {
            mediaError = "No se pudo contactar con la API para eliminar la canción.";
        }
        finally
        {
            deletingMediaId = null;
        }
    }

    private async Task DeleteVisualAssetAsync(VisualAssetResponse item)
    {
        if (!await JavaScript.InvokeAsync<bool>("confirm", $"¿Eliminar el clip «{item.FileName}»? Se borrará también el archivo MP4 local."))
        {
            return;
        }

        deletingMediaId = item.Id;
        mediaError = null;
        try
        {
            var warning = await Api.DeleteVisualAssetAsync(item.Id, CancellationToken.None);
            visualAssets.Remove(item);
            visualAssetCount = Math.Max(0, visualAssetCount - 1);
            if (selectedVisualAssetId == item.Id)
            {
                selectedVisualAssetId = visualAssets.FirstOrDefault()?.Id;
            }

            mediaError = warning;
        }
        catch (ApiResponseException exception)
        {
            mediaError = $"No se pudo eliminar el clip (HTTP {(int)exception.StatusCode}): {exception.Message}";
        }
        catch (HttpRequestException)
        {
            mediaError = "No se pudo contactar con la API para eliminar el clip.";
        }
        finally
        {
            deletingMediaId = null;
        }
    }

    private async Task DeleteRenderedVideoAsync(RenderedVideoResponse item)
    {
        if (!await JavaScript.InvokeAsync<bool>("confirm", $"¿Eliminar el videoclip «{item.Title}»? Se borrará también el MP4 local."))
        {
            return;
        }

        deletingMediaId = item.Id;
        mediaError = null;
        try
        {
            var warning = await Api.DeleteRenderedVideoAsync(item.Id, CancellationToken.None);
            renderedVideos.Remove(item);
            renderedVideoCount = Math.Max(0, renderedVideoCount - 1);
            if (renderedVideo?.Id == item.Id)
            {
                renderedVideo = null;
            }

            mediaError = warning;
        }
        catch (ApiResponseException exception)
        {
            mediaError = $"No se pudo eliminar el videoclip (HTTP {(int)exception.StatusCode}): {exception.Message}";
        }
        catch (HttpRequestException)
        {
            mediaError = "No se pudo contactar con la API para eliminar el videoclip.";
        }
        finally
        {
            deletingMediaId = null;
        }
    }

    private async Task CreateVideoRenderAsync()
    {
        mediaError = null;
        if (selectedMusicTrackId is not Guid trackId || selectedVisualAssetId is not Guid visualId)
        {
            mediaError = "Selecciona una canción y un clip MP4.";
            return;
        }

        var selectedTrack = tracks.FirstOrDefault(item => item.Id == trackId);
        if (selectedTrack?.MusicProjectId is not Guid projectId)
        {
            mediaError = "La canción seleccionada no tiene un proyecto de origen para guardar el videoclip.";
            return;
        }

        renderingVideo = true;
        renderedVideo = null;
        track = null;
        errorMessage = null;
        try
        {
            job = await Api.CreateRenderedVideoAsync(
                projectId,
                trackId,
                visualId,
                string.IsNullOrWhiteSpace(videoTitle) ? selectedTrack.Title : videoTitle,
                CancellationToken.None);
            videoTitle = string.Empty;
            StartProgressPolling(job.Id);
        }
        catch (ApiResponseException exception)
        {
            mediaError = $"La API respondió con HTTP {(int)exception.StatusCode}: {exception.Message}";
        }
        catch (HttpRequestException)
        {
            mediaError = "No se pudo encolar el render del videoclip. Comprueba que la API siga iniciada.";
        }
        finally
        {
            renderingVideo = false;
        }
    }

    private void StartProgressPolling(Guid jobId) =>
        jobPollingTask = PollJobUntilTerminalAsync(jobId, componentLifetime.Token);

    private async Task PollJobUntilTerminalAsync(Guid jobId, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(JobPollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var shouldContinue = false;
                await InvokeAsync(async () =>
                {
                    if (job?.Id != jobId)
                    {
                        return;
                    }

                    await RefreshAsync(cancellationToken);
                    StateHasChanged();
                    shouldContinue = job?.Id == jobId && job.Status is "Queued" or "Running" or "Retrying";
                });

                if (!shouldContinue)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Navegar fuera de la página o detener la aplicación finaliza el sondeo.
        }
        catch (ObjectDisposedException)
        {
            // El circuito puede cerrarse mientras se espera la siguiente actualización.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await componentLifetime.CancelAsync();
        if (jobPollingTask is not null)
        {
            await jobPollingTask;
        }

        componentLifetime.Dispose();
    }

    private string ProjectGenre(Guid? projectId) =>
        projects.FirstOrDefault(item => item.Id == projectId)?.Genre is { } genre
            ? DisplayGenre(genre)
            : "Proyecto musical";

    private void ResetForm()
    {
        form.Title = string.Empty;
        form.Genre = string.Empty;
        form.Mood = null;
        form.PreferredKey = null;
        form.PreferredScale = null;
        form.PreferredGroove = null;
        form.PreferredForm = null;
        form.TextureDensity = null;
        form.MixProfile = null;
        form.ChordsGainDb = 0;
        form.BassGainDb = 0;
        form.LeadGainDb = 0;
        form.DrumsGainDb = 0;
        form.ChordsPan = 0;
        form.BassPan = 0;
        form.LeadPan = 0;
        form.DrumsPan = 0;
        form.TargetDurationSeconds = 60;
        form.UseAI = false;
    }

    private static string DisplayGenre(string? genre) => genre?.ToLowerInvariant() switch
    {
        "lofi" => "Lo-fi",
        "ambient" => "Ambient",
        "electronic" => "Electrónica",
        "classical" => "Clásica",
        "hip-hop" => "Hip-hop",
        "cinematic" => "Cinemática",
        "orchestral" => "Orquestal",
        "latin" => "Latina",
        "rnb" => "R&B",
        null or "" => "Instrumental",
        _ => char.ToUpperInvariant(genre[0]) + genre[1..]
    };

    private static string GenreSymbol(string? genre) => genre?.ToLowerInvariant() switch
    {
        "ambient" or "lofi" => "◌",
        "classical" or "orchestral" => "♬",
        "electronic" or "rnb" => "∿",
        _ => "♫"
    };

    private static int CoverVariant(Guid id) => id.ToString("N")[0] % 4;

    private static string FormatDuration(long? milliseconds)
    {
        if (milliseconds is null or <= 0)
        {
            return "Duración pendiente";
        }

        var duration = TimeSpan.FromMilliseconds(milliseconds.Value);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1_024 => $"{bytes} B",
        < 1_048_576 => $"{bytes / 1_024d:0.#} KB",
        < 1_073_741_824 => $"{bytes / 1_048_576d:0.#} MB",
        _ => $"{bytes / 1_073_741_824d:0.##} GB"
    };

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

        public string? PreferredKey { get; set; }

        public string? PreferredScale { get; set; }

        public string? PreferredGroove { get; set; }

        public string? PreferredForm { get; set; }

        public string? TextureDensity { get; set; }

        public string? MixProfile { get; set; }

        public double ChordsGainDb { get; set; }
        public double BassGainDb { get; set; }
        public double LeadGainDb { get; set; }
        public double DrumsGainDb { get; set; }
        public double ChordsPan { get; set; }
        public double BassPan { get; set; }
        public double LeadPan { get; set; }
        public double DrumsPan { get; set; }

        [Range(1, 4800)]
        public int TargetDurationSeconds { get; set; } = 60;

        public bool UseAI { get; set; }
    }
}
