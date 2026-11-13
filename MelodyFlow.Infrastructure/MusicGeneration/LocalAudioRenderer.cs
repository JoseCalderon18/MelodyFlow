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
        IReadOnlyList<MidiStemSource> stems,
        string workingDirectory,
        string mp3Path,
        int targetDurationSeconds,
        ArrangementPlan plan,
        Func<int, CancellationToken, Task> reportProgressAsync,
        CancellationToken cancellationToken)
    {
        if (stems.Count != 4 || stems.Select(stem => stem.Role).Distinct(StringComparer.Ordinal).Count() != 4)
        {
            throw new MusicGenerationException(
                "audio_stems_invalid",
                "The arrangement did not produce the four required audio parts.",
                isTransient: false);
        }

        var settings = options.Value;
        var (roomSize, reverbLevel, reverbDamp, reverbWidth, chorusActive, chorusLevel, chorusDepth) =
            EffectsFor(plan.MixProfile);
        var wavPaths = new List<string>(stems.Count);
        for (var index = 0; index < stems.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stem = stems[index];
            var wavPath = Path.Combine(workingDirectory, $"stem-{stem.Role}.wav");
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
                stem.MidiPath
            };
            // Cada parte se renderiza sola para que los faders y el paneo actúen sobre audio independiente.
            await RunToolAsync(settings.FluidSynthPath, fluidsynthArguments, cancellationToken);
            wavPaths.Add(wavPath);
            await reportProgressAsync(45 + ((index + 1) * 9), cancellationToken);
        }

        var fadeSeconds = Math.Min(1.8, targetDurationSeconds * 0.18);
        var fadeOutStart = Math.Max(0, targetDurationSeconds - fadeSeconds);
        var filterParts = new List<string>();
        for (var index = 0; index < stems.Count; index++)
        {
            var stem = stems[index];
            filterParts.Add(
                $"[{index}:a]volume={FormatFluidValue(plan.Mixer.GainFor(stem.Role))}dB," +
                $"stereotools=balance_out={FormatFluidValue(plan.Mixer.PanFor(stem.Role))}:bmode_out=power[a{index}]");
        }

        var inputs = string.Concat(Enumerable.Range(0, stems.Count).Select(index => $"[a{index}]"));
        var mixGraph = string.Join(";", filterParts) + ";" +
            $"{inputs}amix=inputs={stems.Count}:duration=longest:dropout_transition=0:normalize=0," +
            "alimiter=limit=0.89:attack=5:release=80:level=0," +
            $"afade=t=in:st=0:d={FormatFluidValue(fadeSeconds)}:curve=hsin," +
            $"afade=t=out:st={FormatFluidValue(fadeOutStart)}:d={FormatFluidValue(fadeSeconds)}:curve=hsin[mix]";
        var ffmpegArguments = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y"
        };
        foreach (var wavPath in wavPaths)
        {
            ffmpegArguments.Add("-i");
            ffmpegArguments.Add(wavPath);
        }

        ffmpegArguments.AddRange(
        [
            "-filter_complex", mixGraph, "-map", "[mix]",
            "-t", targetDurationSeconds.ToString(CultureInfo.InvariantCulture),
            "-codec:a", "libmp3lame", "-q:a", "2", "-ar", "44100", "-map_metadata", "-1", mp3Path
        ]);

        // Suma las partes con controles independientes y limita solo los picos antes del MP3.
        await RunToolAsync(settings.FfmpegPath, ffmpegArguments, cancellationToken);
        await reportProgressAsync(88, cancellationToken);

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
