using MelodyFlow.Application.Music;
using MelodyFlow.Contracts.V1.MusicTracks;
using Microsoft.AspNetCore.Mvc;

namespace MelodyFlowAPI.Controllers;

/// <summary>Expone metadatos de canciones generadas y la descarga de su audio.</summary>
[ApiController]
[Route("api/v1")]
public sealed class MusicTracksController(
    MusicTrackApplicationService tracks,
    IArtifactStorage artifactStorage) : ControllerBase
{
    [HttpGet("generation-jobs/{generationJobId:guid}/track")]
    [ProducesResponseType<MusicTrackResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MusicTrackResponse>> GetByGenerationJobId(
        Guid generationJobId,
        CancellationToken cancellationToken)
    {
        // Cada resultado del caso de uso se traduce a un estado HTTP que el cliente puede manejar.
        var result = await tracks.GetByGenerationJobIdAsync(generationJobId, cancellationToken);
        if (result.Outcome == GenerationJobTrackOutcome.JobNotFound)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Generation job not found");
        }

        if (result.Outcome == GenerationJobTrackOutcome.NotReady)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Audio track is not ready",
                detail: "Check the generation job status and try again after it completes.");
        }

        if (result.Track is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Generated audio track not found");
        }

        var track = result.Track;
        return Ok(new MusicTrackResponse(
            track.Id,
            track.MusicProjectId,
            track.GenerationJobId,
            track.Title,
            track.DurationMs,
            track.Format,
            $"/api/v1/music-tracks/{track.Id:D}/audio",
            track.CreatedAt,
            track.UsedAI));
    }

    [HttpGet("music-tracks/{id:guid}/audio", Name = "GetMusicTrackAudio")]
    [Produces("audio/mpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> DownloadAudio(Guid id, CancellationToken cancellationToken)
    {
        // PostgreSQL conserva la clave y los metadatos; el archivo se transmite desde almacenamiento.
        var track = await tracks.GetByIdAsync(id, cancellationToken);
        if (track is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Audio track not found");
        }

        if (!string.Equals(track.Format, "mp3", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Audio format is not available",
                detail: "This endpoint currently serves MP3 tracks.");
        }

        var audio = await artifactStorage.OpenReadAsync(track.StorageKey, cancellationToken);
        if (audio is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Audio file not found");
        }

        return File(audio, "audio/mpeg", $"melodyflow-{track.Id:N}.mp3", enableRangeProcessing: true);
    }
}
