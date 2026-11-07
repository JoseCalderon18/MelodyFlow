using System.Diagnostics;
using System.Globalization;
using MelodyFlow.Application.Music;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>
/// Ejecuta FluidSynth y FFmpeg instalados localmente sin abrir una consola de comandos.
/// </summary>
internal sealed class LocalAudioRenderer(IOptions<MusicGenerationOptions> options)
{
    /// <summary>Renderiza MIDI a WAV, convierte a MP3 y devuelve la duración medida del archivo final.</summary>
    public async Task<long> RenderMp3Async(
        string midiPath,
        string wavPath,
        string mp3Path,
        int targetDurationSeconds,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        // Indicamos WAV de forma explícita: sin esta opción FluidSynth genera muestras sin cabecera.
        await RunToolAsync(
            settings.FluidSynthPath,
            ["-n", "-i", "-q", "-T", "wav", "-F", wavPath, "-r", "44100", settings.SoundFontPath, midiPath],
            cancellationToken);

        // Limita la duración y elimina metadatos heredados del archivo de salida.
        await RunToolAsync(
            settings.FfmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", wavPath,
                "-t", targetDurationSeconds.ToString(CultureInfo.InvariantCulture),
                "-codec:a", "libmp3lame", "-q:a", "2", "-map_metadata", "-1", mp3Path
            ],
            cancellationToken);

        // La duración se mide sobre el MP3 final y no se deduce solo de la duración solicitada.
        var durationOutput = await RunToolAsync(
            settings.FfprobePath,
            [
                "-v", "error", "-show_entries", "format=duration", "-of",
                "default=noprint_wrappers=1:nokey=1", mp3Path
            ],
            cancellationToken);

        if (!double.TryParse(durationOutput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) ||
            !double.IsFinite(duration) || duration <= 0)
        {
            throw new MusicGenerationException(
                "audio_duration_invalid",
                "The rendered MP3 did not have a measurable positive duration.",
                isTransient: false);
        }

        return checked((long)Math.Round(duration * 1_000, MidpointRounding.AwayFromZero));
    }

    private async Task<string> RunToolAsync(
        string configuredExecutable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var executable = ExecutableResolver.Resolve(configuredExecutable);
        if (executable is null)
        {
            throw new MusicGenerationException(
                "audio_tool_missing",
                "A configured local audio tool could not be found. Check the Worker audio tool paths.",
                isTransient: false);
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        // ArgumentList pasa cada valor separado y evita interpretar rutas como comandos de shell.
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The audio process did not start.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new MusicGenerationException(
                "audio_tool_start_failed",
                "A local audio tool could not be started. Check its installation and configured path.",
                isTransient: false,
                exception);
        }

        // El límite de tiempo se combina con la cancelación del trabajo del Worker.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.ProcessTimeoutSeconds));
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var standardOutput = process.StandardOutput.ReadToEndAsync(linkedCancellation.Token);
        var standardError = process.StandardError.ReadToEndAsync(linkedCancellation.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token);
            var output = await standardOutput;
            var error = await standardError;
            if (process.ExitCode != 0)
            {
                // El diagnóstico puede contener rutas locales; no se copia al estado público del trabajo.
                throw new MusicGenerationException(
                    "audio_tool_failed",
                    "A local audio tool could not render the requested audio. Check the SoundFont and tool configuration.",
                    isTransient: false,
                    new InvalidOperationException($"Process exited with code {process.ExitCode}; diagnostic length {error.Length}."));
            }

            return output;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await TryKillProcessAsync(process);
            throw new MusicGenerationException(
                "audio_tool_timeout",
                "A local audio tool exceeded its configured time limit.",
                isTransient: true);
        }
        catch (OperationCanceledException)
        {
            await TryKillProcessAsync(process);
            throw;
        }
    }

    private static async Task TryKillProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        catch (InvalidOperationException)
        {
            // El proceso pudo terminar entre comprobar su estado e intentar detenerlo.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // La limpieza es secundaria; se conserva como error principal la cancelación o el timeout.
        }
    }
}
