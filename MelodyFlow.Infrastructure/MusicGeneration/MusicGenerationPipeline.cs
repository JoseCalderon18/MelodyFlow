using System.Text.Json;
using MelodyFlow.Application.Music;
using MelodyFlow.Domain.Entities;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>
/// Coordina el planificador, el compositor MIDI determinista y las herramientas de audio locales.
/// </summary>
internal sealed class MusicGenerationPipeline(
    IArrangementPlanProvider planProvider,
    IMidiComposer midiComposer,
    LocalAudioRenderer audioRenderer,
    IArtifactStorage artifactStorage,
    IOptions<MusicGenerationOptions> options) : IMusicGenerationPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MusicTrack> GenerateAsync(
        Guid generationJobId,
        MusicProject project,
        string requestedParametersJson,
        Func<int, CancellationToken, Task> reportProgressAsync,
        CancellationToken cancellationToken)
    {
        // El progreso permite consultar desde la API en qué etapa está el trabajo largo.
        await reportProgressAsync(5, cancellationToken);
        var plan = await planProvider.CreatePlanAsync(
            generationJobId,
            project,
            requestedParametersJson,
            cancellationToken);
        await reportProgressAsync(20, cancellationToken);

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "MelodyFlow", Guid.NewGuid().ToString("N"));
        var midiPath = Path.Combine(temporaryDirectory, "arrangement.mid");
        var wavPath = Path.Combine(temporaryDirectory, "render.wav");
        var mp3Path = Path.Combine(temporaryDirectory, "track.mp3");
        var storageKey = $"music/{project.Id:D}/{generationJobId:D}.mp3";

        try
        {
            // MIDI, WAV y MP3 son temporales; solo el MP3 final se copia al almacenamiento compartido.
            Directory.CreateDirectory(temporaryDirectory);
            await midiComposer.ComposeAsync(plan, project.TargetDurationSeconds, midiPath, cancellationToken);
            await reportProgressAsync(45, cancellationToken);

            var durationMs = await audioRenderer.RenderMp3Async(
                midiPath,
                wavPath,
                mp3Path,
                project.TargetDurationSeconds,
                cancellationToken);
            await reportProgressAsync(85, cancellationToken);

            await using (var mp3 = new FileStream(
                mp3Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await artifactStorage.SaveAsync(storageKey, mp3, cancellationToken);
            }

            await reportProgressAsync(95, cancellationToken);
            // El plan queda junto a los metadatos para explicar y reproducir el resultado.
            var planJson = JsonSerializer.Serialize(plan, JsonOptions);
            return new MusicTrack(
                project.Id,
                generationJobId,
                project.Title,
                durationMs,
                storageKey,
                "mp3",
                providerAssetId: null,
                licenseEvidenceKey: options.Value.SoundFontLicenseEvidenceKey,
                arrangementPlanJson: planJson,
                usedAI: plan.UsedAI);
        }
        catch (MusicGenerationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new MusicGenerationException(
                "midi_generation_failed",
                "The arrangement could not be represented as a supported MIDI file.",
                isTransient: false,
                exception);
        }
        catch (IOException exception)
        {
            throw new MusicGenerationException(
                "media_storage_failed",
                "The generated audio could not be saved to the configured media folder.",
                isTransient: true,
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new MusicGenerationException(
                "media_storage_access_denied",
                "The Worker cannot access its configured temporary or media folder.",
                isTransient: false,
                exception);
        }
        finally
        {
            // Los archivos intermedios no forman parte del catálogo de canciones.
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // La limpieza del temporal es secundaria y no cambia el resultado del trabajo.
            }
            catch (UnauthorizedAccessException)
            {
                // La limpieza del temporal es secundaria y no cambia el resultado del trabajo.
            }
        }
    }
}
