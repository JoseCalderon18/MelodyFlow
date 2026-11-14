using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MelodyFlow.Application.Music;
using MelodyFlow.Infrastructure.MusicGeneration;
using MelodyFlow.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace MelodyFlow.Infrastructure.VideoRendering;

public sealed class LocalVideoRenderer(
    IArtifactStorage storage,
    MediaStoragePaths mediaPaths,
    IOptions<VideoRenderingOptions> options)
{
    public async Task<VideoRenderArtifact> RenderAsync(
        string visualStorageKey,
        IReadOnlyList<VideoMusicSource> musicTracks,
        string outputStorageKey,
        long targetDurationMs,
        Func<int, CancellationToken, Task> reportProgressAsync,
        CancellationToken cancellationToken)
    {
        if (musicTracks.Count is < 1 or > 20 || targetDurationMs is <= 0 or > 7_200_000 ||
            musicTracks.Any(track => track.DurationMs < 15_000))
        {
            throw new VideoRenderingException(
                "video_music_selection_invalid",
                "Choose 1 to 20 songs, each at least 15 seconds long, and a video duration up to 2 hours.",
                isTransient: false);
        }

        var settings = options.Value;
        var temporaryDirectory = Path.Combine(mediaPaths.TemporaryRoot, "video-renders", Guid.NewGuid().ToString("N"));
        var visualPath = Path.Combine(temporaryDirectory, "visual.mp4");
        var musicPaths = musicTracks.Select((_, index) => Path.Combine(temporaryDirectory, $"music-{index:D2}.mp3")).ToArray();
        var playlistAudioPath = Path.Combine(temporaryDirectory, "music-playlist.flac");
        var outputPath = Path.Combine(temporaryDirectory, "rendered.mp4");
        var targetSeconds = targetDurationMs / 1_000d;
        var playlistSeconds = Math.Min(
            targetSeconds,
            musicTracks.Sum(track => track.DurationMs / 1_000d) - 1.5d * Math.Max(0, musicTracks.Count - 1));

        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            await CopyStoredFileAsync(visualStorageKey, visualPath, cancellationToken);
            for (var index = 0; index < musicTracks.Count; index++)
            {
                await CopyStoredFileAsync(musicTracks[index].StorageKey, musicPaths[index], cancellationToken);
            }

            await reportProgressAsync(25, cancellationToken);

            var playlistArguments = new List<string>
            {
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-progress", "pipe:1", "-nostats"
            };
            foreach (var musicPath in musicPaths)
            {
                playlistArguments.Add("-i");
                playlistArguments.Add(musicPath);
            }

            playlistArguments.AddRange(
            [
                "-filter_complex", BuildAudioPlaylistFilter(musicTracks.Count, playlistSeconds),
                "-map", "[audioout]", "-c:a", "flac", "-sample_fmt", "s16", "-ar", "44100", "-ac", "2",
                playlistAudioPath
            ]);

            await RunToolAsync(settings.FfmpegPath, playlistArguments, cancellationToken, async line =>
            {
                if (TryReadProgressSeconds(line, out var elapsedSeconds))
                {
                    var fraction = Math.Clamp(elapsedSeconds / playlistSeconds, 0, 1);
                    await reportProgressAsync(25 + (int)Math.Round(fraction * 12), cancellationToken);
                }
            });

            await reportProgressAsync(40, cancellationToken);
            var renderArguments = new List<string>
            {
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-progress", "pipe:1", "-nostats",
                "-stream_loop", "-1", "-i", visualPath,
                "-stream_loop", "-1", "-i", playlistAudioPath,
                "-map", "0:v:0", "-map", "1:a:0",
                "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",
                "-af", BuildFinalAudioFilter(targetSeconds),
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "192k", "-t", targetSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                "-movflags", "+faststart", outputPath
            };

            await RunToolAsync(settings.FfmpegPath, renderArguments, cancellationToken, async line =>
            {
                if (TryReadProgressSeconds(line, out var elapsedSeconds))
                {
                    var fraction = Math.Clamp(elapsedSeconds / targetSeconds, 0, 1);
                    var percent = 40 + (int)Math.Round(fraction * 50);
                    await reportProgressAsync(Math.Clamp(percent, 25, 90), cancellationToken);
                }
            });

            var metadata = await ProbeOutputAsync(settings.FfprobePath, outputPath, cancellationToken);
            await using (var rendered = new FileStream(
                outputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await storage.SaveAsync(outputStorageKey, rendered, cancellationToken);
            }
            await reportProgressAsync(95, cancellationToken);

            return metadata with { FileSizeBytes = new FileInfo(outputPath).Length };
        }
        catch (VideoRenderingException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new VideoRenderingException(
                "video_storage_or_probe_failed",
                "The video files could not be prepared or inspected. Check the local media storage and FFprobe.",
                isTransient: exception is IOException,
                exception);
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // A leftover temporary file does not change a completed render.
            }
            catch (UnauthorizedAccessException)
            {
                // A leftover temporary file does not change a completed render.
            }
        }
    }

    private static string BuildAudioPlaylistFilter(int inputCount, double playlistSeconds)
    {
        const double crossfadeSeconds = 1.5;
        var filters = new List<string>(inputCount + 2);
        for (var index = 0; index < inputCount; index++)
        {
            filters.Add(
                $"[{index}:a]aresample=44100," +
                "aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo," +
                $"asetpts=PTS-STARTPTS[a{index}]");
        }

        var current = "a0";
        for (var index = 1; index < inputCount; index++)
        {
            var next = $"xfade{index}";
            filters.Add($"[{current}][a{index}]acrossfade=d={crossfadeSeconds.ToString("0.0", CultureInfo.InvariantCulture)}:c1=tri:c2=tri[{next}]");
            current = next;
        }

        // A short edge fade softens the seam when the whole playlist must loop for a long video.
        var edgeFadeSeconds = Math.Min(0.75d, playlistSeconds * 0.05d);
        var fadeOutStart = Math.Max(0d, playlistSeconds - edgeFadeSeconds);
        filters.Add(
            $"[{current}]atrim=duration={playlistSeconds.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"afade=t=in:st=0:d={edgeFadeSeconds.ToString("0.###", CultureInfo.InvariantCulture)}:curve=hsin," +
            $"afade=t=out:st={fadeOutStart.ToString("0.###", CultureInfo.InvariantCulture)}:d={edgeFadeSeconds.ToString("0.###", CultureInfo.InvariantCulture)}:curve=hsin[audioout]");
        return string.Join(';', filters);
    }

    private static string BuildFinalAudioFilter(double targetSeconds)
    {
        var fadeSeconds = Math.Min(5d, targetSeconds * 0.05d);
        var fadeStart = Math.Max(0d, targetSeconds - fadeSeconds);
        return $"afade=t=out:st={fadeStart.ToString("0.###", CultureInfo.InvariantCulture)}:d={fadeSeconds.ToString("0.###", CultureInfo.InvariantCulture)}:curve=hsin";
    }

    private async Task CopyStoredFileAsync(string storageKey, string destinationPath, CancellationToken cancellationToken)
    {
        var stored = await storage.OpenReadAsync(storageKey, cancellationToken);
        if (stored is null)
        {
            throw new VideoRenderingException(
                "video_input_missing",
                "A selected clip or music file is missing from local media storage.",
                isTransient: false);
        }

        await using (stored)
        await using (var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await stored.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
    }

    private async Task<VideoRenderArtifact> ProbeOutputAsync(
        string executable,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var json = await RunToolAsync(executable,
        [
            "-v", "error", "-show_entries", "format=duration:stream=codec_type,codec_name,width,height",
            "-of", "json", outputPath
        ], cancellationToken);

        using var document = JsonDocument.Parse(json);
        var streams = document.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var stream = streams
            .FirstOrDefault(item => item.TryGetProperty("codec_type", out var type) && type.GetString() == "video");
        var hasAudio = streams.Any(item => item.TryGetProperty("codec_type", out var type) && type.GetString() == "audio");
        if (stream.ValueKind != JsonValueKind.Object || !hasAudio)
        {
            throw new VideoRenderingException(
                "video_output_invalid",
                "FFmpeg did not produce a playable MP4 with both video and audio streams.",
                isTransient: false);
        }

        var format = document.RootElement.GetProperty("format");
        if (!double.TryParse(format.GetProperty("duration").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) ||
            !double.IsFinite(duration) || duration <= 0)
        {
            throw new VideoRenderingException(
                "video_duration_invalid",
                "The rendered MP4 has no measurable duration.",
                isTransient: false);
        }

        return new VideoRenderArtifact(
            checked((long)Math.Round(duration * 1_000, MidpointRounding.AwayFromZero)),
            stream.GetProperty("width").GetInt32(),
            stream.GetProperty("height").GetInt32(),
            stream.GetProperty("codec_name").GetString() ?? "h264",
            0);
    }

    private async Task<string> RunToolAsync(
        string configuredExecutable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        Func<string, Task>? onStandardOutputLineAsync = null)
    {
        var executable = ExecutableResolver.Resolve(configuredExecutable);
        if (executable is null)
        {
            throw new VideoRenderingException(
                "video_tool_missing",
                "FFmpeg or FFprobe is no longer available on the Worker machine.",
                isTransient: false);
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The video process did not start.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new VideoRenderingException(
                "video_tool_start_failed",
                "A local video tool could not be started.",
                isTransient: false,
                exception);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.ProcessTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(linked.Token);
        try
        {
            var output = new StringBuilder();
            while (await process.StandardOutput.ReadLineAsync(linked.Token) is { } line)
            {
                output.AppendLine(line);
                if (onStandardOutputLineAsync is not null)
                {
                    await onStandardOutputLineAsync(line);
                }
            }

            await process.WaitForExitAsync(linked.Token);
            var error = await errorTask;
            if (process.ExitCode != 0)
            {
                throw new VideoRenderingException(
                    "video_tool_failed",
                    "FFmpeg could not combine the selected MP4 clip and music tracks.",
                    isTransient: false,
                    new InvalidOperationException($"Local media process failed; diagnostic length {error.Length}."));
            }

            return output.ToString();
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process ended between the state check and Kill.
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new VideoRenderingException(
                "video_tool_timeout",
                "The video render exceeded its local processing time limit.",
                isTransient: true);
        }
    }

    private static bool TryReadProgressSeconds(string line, out double elapsedSeconds)
    {
        elapsedSeconds = 0;
        var separator = line.IndexOf('=');
        if (separator < 0 || !line.StartsWith("out_time_us=", StringComparison.Ordinal))
        {
            return false;
        }

        return long.TryParse(line[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) &&
            microseconds >= 0 &&
            (elapsedSeconds = microseconds / 1_000_000d) >= 0;
    }
}

public sealed record VideoMusicSource(string StorageKey, long DurationMs);

public sealed record VideoRenderArtifact(long DurationMs, int WidthPx, int HeightPx, string Codec, long FileSizeBytes);

public sealed class VideoRenderingException(
    string errorCode,
    string safeMessage,
    bool isTransient,
    Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public string ErrorCode { get; } = errorCode;
    public string SafeMessage { get; } = safeMessage;
    public bool IsTransient { get; } = isTransient;
}
