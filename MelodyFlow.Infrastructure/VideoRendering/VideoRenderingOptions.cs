using MelodyFlow.Infrastructure.MusicGeneration;

namespace MelodyFlow.Infrastructure.VideoRendering;

/// <summary>Herramientas locales requeridas para ensamblar clips y música con FFmpeg.</summary>
public sealed class VideoRenderingOptions
{
    public string FfmpegPath { get; init; } = "ffmpeg";
    public string FfprobePath { get; init; } = "ffprobe";
    public int ProcessTimeoutSeconds { get; init; } = 28_800;

    public IReadOnlyList<string> GetConfigurationProblems()
    {
        var problems = new List<string>();
        if (!ExecutableResolver.Exists(FfmpegPath))
        {
            problems.Add("VideoRendering:FfmpegPath must resolve to FFmpeg.");
        }

        if (!ExecutableResolver.Exists(FfprobePath))
        {
            problems.Add("VideoRendering:FfprobePath must resolve to FFprobe.");
        }

        if (ProcessTimeoutSeconds is < 30 or > 86_400)
        {
            problems.Add("VideoRendering:ProcessTimeoutSeconds must be between 30 and 86400.");
        }

        return problems;
    }
}
