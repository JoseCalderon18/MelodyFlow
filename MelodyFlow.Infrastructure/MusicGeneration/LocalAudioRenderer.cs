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
        ArrangementPlan plan,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var (roomSize, reverbLevel, reverbDamp, reverbWidth, chorusActive, chorusLevel, chorusDepth) =
            EffectsFor(plan.MixProfile);
        var fluidsynthArguments = new List<string>
        {
            "-n", "-i", "-q", "-T", "wav", "-F", wavPath, "-r", "44100",
            "-o", "synth.gain=0.18",
            "-o", "synth.reverb.active=1",
            "-o", $"synth.reverb.room-size={FormatFluidValue(roomSize)}",
            "-o", $"synth.reverb.level={FormatFluidValue(reverbLevel)}",
            "-o", $"synth.reverb.damp={FormatFluidValue(reverbDamp)}",
            "-o", $"synth.reverb.width={FormatFluidValue(reverbWidth)}",
            "-o", $"synth.chorus.active={(chorusActive ? 1 : 0)}",
            "-o", $"synth.chorus.level={FormatFluidValue(chorusLevel)}",
            "-o", "synth.chorus.nr=3",
            "-o", "synth.chorus.speed=0.25",
            "-o", $"synth.chorus.depth={FormatFluidValue(chorusDepth)}",
            settings.SoundFontPath,
            midiPath
        };
        // Indicamos WAV de forma explícita: sin esta opción FluidSynth genera muestras sin cabecera.
        await RunToolAsync(
            settings.FluidSynthPath,
            fluidsynthArguments,
            cancellationToken);

        var fadeSeconds = Math.Min(1.8, targetDurationSeconds * 0.18);
        var fadeOutStart = Math.Max(0, targetDurationSeconds - fadeSeconds);
        var fadeFilter = string.Join(",",
            $"afade=t=in:st=0:d={FormatFluidValue(fadeSeconds)}:curve=hsin",
            $"afade=t=out:st={FormatFluidValue(fadeOutStart)}:d={FormatFluidValue(fadeSeconds)}:curve=hsin");

        // Conserva el rango dinámico y añade fades cortos para evitar cortes secos en los extremos.
        await RunToolAsync(
            settings.FfmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", wavPath,
                "-t", targetDurationSeconds.ToString(CultureInfo.InvariantCulture),
                "-af", fadeFilter,
                "-codec:a", "libmp3lame", "-q:a", "2", "-ar", "44100", "-map_metadata", "-1", mp3Path
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

    private static (double Room, double Reverb, double Damp, double Width, bool Chorus, double ChorusLevel, double ChorusDepth)
        EffectsFor(string mixProfile) => mixProfile switch
        {
            "intimate" => (0.18, 0.26, 0.72, 0.45, false, 0, 4.0),
            "warm" => (0.28, 0.36, 0.62, 0.65, true, 0.16, 5.0),
            "spacious" => (0.48, 0.45, 0.56, 0.85, true, 0.28, 6.0),
            _ => (0.30, 0.32, 0.66, 0.72, true, 0.18, 4.5)
        };

    private static string FormatFluidValue(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

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
