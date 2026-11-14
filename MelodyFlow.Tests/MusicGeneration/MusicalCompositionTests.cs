using MelodyFlow.Domain.Entities;
using MelodyFlow.Infrastructure.MusicGeneration;

namespace MelodyFlow.Tests.MusicGeneration;

public sealed class MusicalCompositionTests
{
    [Fact]
    public void CatalogProvides108RootAndScaleCombinations()
    {
        Assert.Equal(9, MusicalScaleCatalog.All.Count);
        Assert.Equal(108, 12 * MusicalScaleCatalog.All.Count);
        Assert.Equal(7, MusicalScaleCatalog.All.Select(scale => scale.Semitones.Count).Distinct().Single());
    }

    [Fact]
    public async Task ComposerWritesFiveDeterministicStyleTracks()
    {
        var project = new MusicProject("Test composition", "lofi", 30, "calm");
        var request = new MusicGenerationRequest(
            UseAI: false,
            VariationSeed: 1234,
            PreferredTempoBpm: null,
            PreferredKey: "A",
            PreferredScale: "dorian");
        var plan = DeterministicArrangementPlanner.Create(project, request, seed: 1234);
        var firstPath = Path.Combine(Path.GetTempPath(), $"melodyflow-{Guid.NewGuid():N}-first.mid");
        var secondPath = Path.Combine(Path.GetTempPath(), $"melodyflow-{Guid.NewGuid():N}-second.mid");

        try
        {
            var composer = new MidiComposer();
            await composer.ComposeAsync(plan, 30, firstPath, CancellationToken.None);
            await composer.ComposeAsync(plan, 30, secondPath, CancellationToken.None);

            var midi = await File.ReadAllBytesAsync(firstPath);
            Assert.Equal("MThd", System.Text.Encoding.ASCII.GetString(midi, 0, 4));
            Assert.Equal(5, System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(midi.AsSpan(10, 2)));
            Assert.Equal("A minor", plan.Key);
            Assert.Equal("dorian", plan.Scale);
            Assert.Equal("lofi", plan.Groove);
            Assert.Equal(await File.ReadAllBytesAsync(firstPath), await File.ReadAllBytesAsync(secondPath));

            var offset = 14;
            for (var index = 0; index < 5; index++)
            {
                Assert.Equal("MTrk", System.Text.Encoding.ASCII.GetString(midi, offset, 4));
                var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(midi.AsSpan(offset + 4, 4));
                offset = checked(offset + 8 + (int)length);
            }

            Assert.Equal(midi.Length, offset);
        }
        finally
        {
            if (File.Exists(firstPath)) File.Delete(firstPath);
            if (File.Exists(secondPath)) File.Delete(secondPath);
        }
    }

    [Fact]
    public async Task ComposerHandlesLongMelodyPhraseTransitions()
    {
        const int durationSeconds = 600;
        var project = new MusicProject("Long composition", "ambient", durationSeconds, "calm");
        var request = new MusicGenerationRequest(
            UseAI: false,
            VariationSeed: 7319,
            PreferredTempoBpm: null,
            PreferredKey: null,
            PreferredScale: null);
        var plan = DeterministicArrangementPlanner.Create(project, request, seed: 7319);
        var midiPath = Path.Combine(Path.GetTempPath(), $"melodyflow-{Guid.NewGuid():N}-long.mid");

        try
        {
            await new MidiComposer().ComposeAsync(plan, durationSeconds, midiPath, CancellationToken.None);

            Assert.True(new FileInfo(midiPath).Length > 0);
        }
        finally
        {
            if (File.Exists(midiPath)) File.Delete(midiPath);
        }
    }
}
