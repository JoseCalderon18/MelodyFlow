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
        string musicStorageKey,
        string outputStorageKey,
        long musicDurationMs,
        Func<int, CancellationToken, Task> reportProgressAsync,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var temporaryDirectory = Path.Combine(mediaPaths.TemporaryRoot, "video-renders", Guid.NewGuid().ToString("N"));
        var visualPath = Path.Combine(temporaryDirectory, "visual.mp4");
        var musicPath = Path.Combine(temporaryDirectory, "music.mp3");
        var outputPath = Path.Combine(temporaryDirectory, "rendered.mp4");

        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            await CopyStoredFileAsync(visualStorageKey, visualPath, cancellationToken);
            await CopyStoredFileAsync(musicStorageKey, musicPath, cancellationToken);
            await reportProgressAsync(25, cancellationToken);

            await RunToolAsync(settings.FfmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
                "-progress", "pipe:1", "-nostats",
                "-stream_loop", "-1", "-i", visualPath,
                "-i", musicPath,
                "-map", "0:v:0", "-map", "1:a:0",
                "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2",
                "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "192k", "-t", (musicDurationMs / 1_000d).ToString("0.###", CultureInfo.InvariantCulture),
                "-movflags", "+faststart", outputPath
            ], cancellationToken, async line =>
            {
                if (TryReadProgressSeconds(line, out var elapsedSeconds))
                {
                    var fraction = Math.Clamp(elapsedSeconds / (musicDurationMs / 1_000d), 0, 1);
                    var percent = 25 + (int)Math.Round(fraction * 65);
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
        var stream = document.RootElement.GetProperty("streams").EnumerateArray()
            .FirstOrDefault(item => item.TryGetProperty("codec_type", out var type) && type.GetString() == "video");
        if (stream.ValueKind != JsonValueKind.Object)
        {
            throw new VideoRenderingException(
                "video_output_invalid",
                "FFmpeg did not produce a video stream that FFprobe can read.",
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
                    "FFmpeg could not combine the selected MP4 clip and music track.",
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
