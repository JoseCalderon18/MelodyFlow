using System.Buffers.Binary;
using System.Text;

namespace MelodyFlow.Infrastructure.MusicGeneration;

internal interface IMidiComposer
{
    Task ComposeAsync(
        ArrangementPlan plan,
        int targetDurationSeconds,
        string destinationPath,
        CancellationToken cancellationToken);
}

/// <summary>Writes a deterministic, style-aware multi-track MIDI arrangement.</summary>
internal sealed class MidiComposer : IMidiComposer
{
    private const int TicksPerQuarterNote = 480;
    private const int BeatsPerBar = 4;
    private const int EighthNoteTicks = TicksPerQuarterNote / 2;

    public async Task ComposeAsync(
        ArrangementPlan plan,
        int targetDurationSeconds,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(plan);
        if (targetDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetDurationSeconds));
        }

        if (!MusicKey.TryParse(plan.Key, out var key) ||
            !MusicalScaleCatalog.TryGet(plan.Scale, out var scale) ||
            !ArrangementStyleCatalog.IsSupportedGroove(plan.Groove) ||
            !ArrangementStyleCatalog.IsSupportedInstrumentSet(plan.InstrumentSet))
        {
            throw new InvalidDataException("The arrangement contains unsupported musical settings.");
        }

        var targetTicks = (long)Math.Ceiling(
            targetDurationSeconds * (double)plan.TempoBpm * TicksPerQuarterNote / 60d);
        var ticksPerBar = TicksPerQuarterNote * BeatsPerBar;
        var barCount = (targetTicks + ticksPerBar - 1) / ticksPerBar;
        var microsecondsPerQuarter = (int)Math.Round(60_000_000d / plan.TempoBpm);
        var sections = ArrangementStructure.Create(barCount, plan.Form);
        var programs = GetPrograms(plan);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using var stream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            WriteAscii(writer, "MThd");
            WriteUInt32BigEndian(writer, 6);
            WriteUInt16BigEndian(writer, 1);
            WriteUInt16BigEndian(writer, 5);
            WriteUInt16BigEndian(writer, TicksPerQuarterNote);

            WriteTrack(writer, track => WriteConductorTrack(track, plan, sections, microsecondsPerQuarter, ticksPerBar), targetTicks);
            WriteTrack(writer, track => WriteChordTrack(track, plan, sections, programs.Chords, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteBassTrack(track, plan, sections, programs.Bass, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteMelodyTrack(track, plan, key, scale, sections, programs.Lead, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteDrumTrack(track, plan, sections, barCount, ticksPerBar, targetTicks), targetTicks);
        }

        await stream.FlushAsync(cancellationToken);
    }

    private static void WriteConductorTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        IReadOnlyList<ArrangementSection> sections,
        int microsecondsPerQuarter,
        long ticksPerBar)
    {
        track.WriteAt(0, 0xFF, 0x51, 0x03,
            (byte)(microsecondsPerQuarter >> 16),
            (byte)(microsecondsPerQuarter >> 8),
            (byte)microsecondsPerQuarter);
        track.WriteAt(0, 0xFF, 0x58, 0x04, BeatsPerBar, 0x02, 24, 8);
        WriteTextEvent(track, 0, $"{plan.Key} | {plan.Scale} | {plan.Groove}");
        foreach (var section in sections.Where(section => section.StartBar > 0))
        {
            WriteTextEvent(track, section.StartBar * ticksPerBar, section.Name);
        }
    }

    private static void WriteChordTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        IReadOnlyList<ArrangementSection> sections,
        int program,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 0;
        track.WriteAt(0, (byte)(0xC0 | channel), (byte)program);
        WriteMixerControls(track, channel, plan, TrackRole.Chords);
        var events = new List<MidiNoteEvent>();
        var controls = CreateExpressionEvents(channel, plan, sections, ticksPerBar, barCount, TrackRole.Chords);
        int[]? previousVoicing = null;

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            var section = ArrangementStructure.At(sections, bar);
            var chordIndex = (ArrangementStructure.ChordOffset(section) + (int)(bar % 4)) % plan.Chords.Count;
            if (!Chord.TryParse(plan.Chords[chordIndex], out var chord))
            {
                throw new InvalidDataException("The arrangement contains an unsupported chord.");
            }

            var voicing = BuildVoicing(chord, previousVoicing, bar, plan.VariationSeed);
            previousVoicing = voicing;
            var intensity = SectionIntensity(section, bar, barCount, plan.TextureDensity);
            if (plan.Groove == "arpeggiated")
            {
                var stride = plan.TextureDensity == "sparse" ? 2 : 1;
                for (var step = 0; step < 8; step += stride)
                {
                    var note = voicing[(step + (int)(bar + (uint)plan.VariationSeed)) % voicing.Length];
                    var noteStart = barStart + (step * EighthNoteTicks);
                    var velocity = ScaleVelocity(69, intensity * (step % 2 == 0 ? 1.0 : 0.78));
                    AddNote(events, note, noteStart, EighthNoteTicks - 30, targetTicks, velocity);
                }
            }
            else
            {
                foreach (var (offset, duration, velocityScale) in ChordRhythm(plan.Groove, bar, plan.VariationSeed, section))
                {
                    var velocity = ScaleVelocity(72, intensity * velocityScale);
                    foreach (var note in voicing)
                    {
                        AddNote(events, note, barStart + offset, duration, targetTicks, velocity);
                    }
                }
            }
        }

        WriteNotes(track, channel, events, controls);
    }

    private static void WriteBassTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        IReadOnlyList<ArrangementSection> sections,
        int program,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 1;
        track.WriteAt(0, (byte)(0xC0 | channel), (byte)program);
        WriteMixerControls(track, channel, plan, TrackRole.Bass);
        var events = new List<MidiNoteEvent>();
        var controls = CreateExpressionEvents(channel, plan, sections, ticksPerBar, barCount, TrackRole.Bass);

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            var section = ArrangementStructure.At(sections, bar);
            var chordIndex = (ArrangementStructure.ChordOffset(section) + (int)(bar % 4)) % plan.Chords.Count;
            if (!Chord.TryParse(plan.Chords[chordIndex], out var chord))
            {
                throw new InvalidDataException("The arrangement contains an unsupported chord.");
            }

            var intensity = SectionIntensity(section, bar, barCount, plan.TextureDensity);
            if (section.Type == ArrangementSectionType.Break ||
                (section.Type == ArrangementSectionType.Intro && bar < section.EndBar - 1) ||
                (plan.TextureDensity == "sparse" && section.Type == ArrangementSectionType.Outro))
            {
                continue;
            }

            foreach (var (beat, degree, durationBeats, velocityScale) in BassRhythm(plan.Groove, bar, plan.VariationSeed))
            {
                var note = 36 + chord.RootPitchClass + degree;
                var velocity = ScaleVelocity(78, intensity * velocityScale);
                AddNote(events, note, barStart + (beat * TicksPerQuarterNote), durationBeats * TicksPerQuarterNote,
                    targetTicks, velocity);
            }
        }

        WriteNotes(track, channel, events, controls);
    }

    private static void WriteMelodyTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        MusicKey key,
        MusicalScaleDefinition scale,
        IReadOnlyList<ArrangementSection> sections,
        int program,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 2;
        track.WriteAt(0, (byte)(0xC0 | channel), (byte)program);
        WriteMixerControls(track, channel, plan, TrackRole.Lead);
        var events = new List<MidiNoteEvent>();
        var controls = CreateExpressionEvents(channel, plan, sections, ticksPerBar, barCount, TrackRole.Lead);
        var random = new Random(plan.VariationSeed ^ 0x2F10A9);

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            var section = ArrangementStructure.At(sections, bar);
            var rhythm = MelodySteps(plan.Groove, bar, plan.VariationSeed, plan.TextureDensity, section);
            var motifOffset = (int)((unchecked((uint)plan.VariationSeed) % 7 +
                (section.Type == ArrangementSectionType.Variation ? 2u : 0u)) % 7);
            var intensity = SectionIntensity(section, bar, barCount, plan.TextureDensity);
            var chordIndex = (ArrangementStructure.ChordOffset(section) + (int)(bar % 4)) % plan.Chords.Count;
            if (!Chord.TryParse(plan.Chords[chordIndex], out var barChord))
            {
                throw new InvalidDataException("The arrangement contains an unsupported chord.");
            }

            foreach (var step in rhythm)
            {
                if (step == 7 && random.Next(0, 4) == 0)
                {
                    continue;
                }

                var degree = MelodyDegree(
                    step,
                    (int)((unchecked((uint)plan.VariationSeed) >> 14) % 4),
                    motifOffset,
                    section.Type,
                    random);
                if (step % 4 == 0)
                {
                    degree = MoveToChordTone(degree, barChord, key, scale);
                }
                var octave = plan.InstrumentSet is "cinematic" or "ambient" ? 1 : 0;
                var note = scale.GetMidiNote(key.RootPitchClass, degree, octaveOffset: octave);
                var velocity = ScaleVelocity(68 + random.Next(0, 18), intensity);
                var start = barStart + (step * EighthNoteTicks);
                var noteLength = plan.Groove is ("ambient" or "pulse") ||
                    section.Type is (ArrangementSectionType.Break or ArrangementSectionType.Outro)
                    ? EighthNoteTicks * 3L
                    : EighthNoteTicks - 36L;
                AddNote(events, note, start, noteLength, targetTicks, velocity);
            }
        }

        WriteNotes(track, channel, events, controls);
    }

    private static void WriteDrumTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        IReadOnlyList<ArrangementSection> sections,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 9;
        WriteMixerControls(track, channel, plan, TrackRole.Drums);
        var events = new List<MidiNoteEvent>();
        var controls = CreateExpressionEvents(channel, plan, sections, ticksPerBar, barCount, TrackRole.Drums);
        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            var section = ArrangementStructure.At(sections, bar);
            var intensity = SectionIntensity(section, bar, barCount, plan.TextureDensity);
            if (section.Type == ArrangementSectionType.Break ||
                (section.Type == ArrangementSectionType.Intro && bar < section.EndBar - 1) ||
                section.Type == ArrangementSectionType.Outro)
            {
                continue;
            }

            var hits = DrumHits(plan.Groove, bar, plan.VariationSeed).ToList();
            if (section.Type == ArrangementSectionType.Return && bar == section.StartBar)
            {
                hits.Add((0, 49, 64));
            }

            foreach (var (step, note, velocity) in hits)
            {
                var offset = step * EighthNoteTicks + SwingOffset(plan.Groove, step);
                AddNote(events, note, barStart + offset, durationTicks: 60,
                    targetTicks, ScaleVelocity(velocity, intensity));
            }
        }

        WriteNotes(track, channel, events, controls);
    }

    private static int[] BuildVoicing(Chord chord, int[]? previousVoicing, long bar, int seed)
    {
        var source = chord.Intervals.Select(interval => 60 + chord.RootPitchClass + interval).Order().ToArray();
        var candidates = new List<int[]>();
        for (var inversion = 0; inversion < source.Length; inversion++)
        {
            var rotated = source.Skip(inversion)
                .Concat(source.Take(inversion).Select(note => note + 12))
                .ToArray();
            foreach (var octaveShift in new[] { -12, 0, 12 })
            {
                var candidate = rotated.Select(note => note + octaveShift).ToArray();
                while (candidate[0] < 48)
                {
                    candidate = candidate.Select(note => note + 12).ToArray();
                }

                while (candidate[^1] > 84)
                {
                    candidate = candidate.Select(note => note - 12).ToArray();
                }

                candidates.Add(candidate);
            }
        }

        var scored = candidates.Select(candidate => new
        {
            Notes = candidate,
            Score = previousVoicing is null
                ? Math.Abs(candidate.Average() - 67) + (candidate[^1] - candidate[0]) * 0.16
                : Enumerable.Range(0, Math.Min(candidate.Length, previousVoicing.Length))
                    .Sum(index => Math.Abs(candidate[index] - previousVoicing[index])) +
                  Math.Abs(candidate.Length - previousVoicing.Length) * 5
        }).ToArray();
        var bestScore = scored.Min(item => item.Score);
        var best = scored.Where(item => item.Score <= bestScore + 2).Select(item => item.Notes).ToArray();
        var choice = (int)((unchecked((uint)bar) + unchecked((uint)seed)) % (uint)best.Length);
        return best[choice];
    }

    private static IEnumerable<(long Offset, long Duration, double VelocityScale)> ChordRhythm(
        string groove,
        long bar,
        int seed,
        ArrangementSection section)
    {
        if (section.Type is ArrangementSectionType.Intro or ArrangementSectionType.Break or ArrangementSectionType.Outro)
        {
            return [(0, TicksPerQuarterNote * 4L - 30, 0.66)];
        }

        return groove switch
        {
            "ambient" or "pulse" => [(0, TicksPerQuarterNote * 4L - 24, 0.78)],
            "cinematic" => [(0, TicksPerQuarterNote * 4L - 24, bar % 8 == 7 ? 1.0 : 0.88)],
            "lofi" => [(0, TicksPerQuarterNote * 2L - 30, 1.0),
                (TicksPerQuarterNote * 2L + (seed % 2 == 0 ? 0 : EighthNoteTicks), TicksPerQuarterNote + 90, 0.78)],
            "four-on-floor" =>
            [
                (EighthNoteTicks, EighthNoteTicks, 0.68),
                (TicksPerQuarterNote + EighthNoteTicks, EighthNoteTicks, 0.62),
                (TicksPerQuarterNote * 2L + EighthNoteTicks, EighthNoteTicks, 0.68),
                (TicksPerQuarterNote * 3L + EighthNoteTicks, EighthNoteTicks, 0.62)
            ],
            "swing" or "shuffle" =>
            [
                (0, TicksPerQuarterNote + 24L, 0.90),
                (TicksPerQuarterNote * 2L + EighthNoteTicks, TicksPerQuarterNote - 24L, 0.72)
            ],
            "broken-beat" =>
            [
                (0, TicksPerQuarterNote - 24L, 0.92),
                (TicksPerQuarterNote + EighthNoteTicks, EighthNoteTicks, 0.60),
                (TicksPerQuarterNote * 2L + EighthNoteTicks, TicksPerQuarterNote - 30L, 0.76),
                (TicksPerQuarterNote * 3L + EighthNoteTicks, EighthNoteTicks, 0.58)
            ],
            "clave" =>
            [
                (0, TicksPerQuarterNote * 2L - 30, 0.90),
                (TicksPerQuarterNote + EighthNoteTicks, EighthNoteTicks, 0.62),
                (TicksPerQuarterNote * 2L + EighthNoteTicks, TicksPerQuarterNote - 30L, 0.78)
            ],
            _ => [(0, TicksPerQuarterNote * 2L - 24, 0.94),
                (TicksPerQuarterNote * 2L, TicksPerQuarterNote - 30, 0.72)]
        };
    }

    private static IEnumerable<(int Beat, int Degree, int DurationBeats, double VelocityScale)> BassRhythm(
        string groove,
        long bar,
        int seed)
    {
        if (groove is "ambient" or "pulse" or "cinematic")
        {
            return bar % 2 == 0
                ? [(0, 0, 4, 0.72)]
                : [(0, 0, 2, 0.82), (2, 4, 2, 0.64)];
        }

        if (groove == "four-on-floor")
        {
            return [(0, 0, 1, 1.0), (1, 0, 1, 0.74), (2, 0, 1, 0.94), (3, bar % 2 == 0 ? 0 : 4, 1, 0.72)];
        }

        if (groove == "lofi")
        {
            return seed % 2 == 0
                ? [(0, 0, 2, 1.0), (2, 4, 1, 0.78), (3, 0, 1, 0.68)]
                : [(0, 0, 2, 1.0), (2, 4, 2, 0.72)];
        }

        if (groove is "swing" or "shuffle")
        {
            return bar % 2 == 0
                ? [(0, 0, 2, 0.94), (2, 4, 1, 0.68), (3, 0, 1, 0.78)]
                : [(0, 0, 1, 0.90), (1, 4, 1, 0.62), (2, 0, 2, 0.82)];
        }

        if (groove == "clave")
        {
            return [(0, 0, 2, 0.94), (2, 4, 1, 0.72), (3, 0, 1, 0.80)];
        }

        if (groove == "broken-beat")
        {
            return [(0, 0, 1, 1.0), (1, 4, 1, 0.62), (2, 0, 1, 0.84), (3, 4, 1, 0.70)];
        }

        return [(0, 0, 1, 1.0), (2, 4, 1, 0.78)];
    }

    private static IReadOnlyList<int> MelodySteps(
        string groove,
        long bar,
        int seed,
        string density,
        ArrangementSection section)
    {
        var pattern = groove switch
        {
            "ambient" => new[] { 0, 3, 6 },
            "pulse" => new[] { 0, 4 },
            "lofi" => new[] { 0, 3, 4, 6 },
            "four-on-floor" => new[] { 1, 3, 5, 7 },
            "cinematic" => bar % 2 == 0 ? new[] { 0, 4, 6 } : new[] { 2, 4, 7 },
            "swing" or "shuffle" => new[] { 0, 2, 3, 4, 6 },
            "broken-beat" => new[] { 0, 2, 5, 6 },
            "clave" => new[] { 0, 3, 4, 6 },
            "arpeggiated" => new[] { 0, 2, 4, 6 },
            _ => new[] { 0, 2, 4, 7 }
        };

        if (density == "sparse" || section.Type is ArrangementSectionType.Intro or ArrangementSectionType.Break or ArrangementSectionType.Outro)
        {
            pattern = pattern.Where((_, index) => index % 2 == 0).ToArray();
        }
        else if (density == "layered" && groove is not ("ambient" or "pulse"))
        {
            pattern = pattern.Concat([1, 5, 7]).Distinct().Order().ToArray();
        }

        var shift = (int)((bar + (uint)seed) % pattern.Length);
        return pattern.Skip(shift).Concat(pattern.Take(shift)).ToArray();
    }

    private static int MelodyDegree(
        int step,
        int motifVariant,
        int motifOffset,
        ArrangementSectionType sectionType,
        Random random)
    {
        int[] motif = motifVariant switch
        {
            0 => [0, 2, 4, 2, 1, 3, 4, 6],
            1 => [4, 3, 2, 0, 2, 1, 0, 1],
            2 => [2, 4, 5, 4, 2, 0, 1, 2],
            _ => [0, 1, 3, 2, 4, 3, 1, 0]
        };

        // Keep one identifiable motif through the song; the B section answers it in reverse
        // with a small transposition, while the return restores its original contour.
        var motifStep = sectionType == ArrangementSectionType.Variation ? 7 - step : step;
        var degree = motif[motifStep] + motifOffset;
        if (random.Next(0, 9) == 0)
        {
            degree += random.Next(0, 2) == 0 ? -1 : 1;
        }

        return degree;
    }

    private static int MoveToChordTone(int targetDegree, Chord chord, MusicKey key, MusicalScaleDefinition scale)
    {
        var chordPitchClasses = chord.Intervals
            .Select(interval => (chord.RootPitchClass + interval) % 12)
            .ToHashSet();
        var candidates = Enumerable.Range(-7, 22)
            .Where(degree => chordPitchClasses.Contains(((scale.GetMidiNote(key.RootPitchClass, degree) % 12) + 12) % 12))
            .ToArray();
        return candidates.Length == 0
            ? targetDegree
            : candidates.OrderBy(degree => Math.Abs(degree - targetDegree)).ThenBy(degree => degree).First();
    }

    private static IReadOnlyList<(int Step, int Note, int Velocity)> DrumHits(string groove, long bar, int seed)
    {
        var hits = new List<(int Step, int Note, int Velocity)>();
        switch (groove)
        {
            case "ambient":
                if (bar % 4 == 3) hits.Add((0, 49, 30));
                break;
            case "pulse":
                if (bar % 2 == 0) hits.Add((0, 36, 40));
                if (bar % 4 == 3) hits.Add((6, 51, 24));
                break;
            case "cinematic":
                if (bar % 2 == 0) hits.Add((0, 35, 58));
                if (bar % 4 == 3) hits.Add((0, 49, 50));
                if (bar % 8 == 7) hits.Add((6, 38, 42));
                break;
            case "lofi":
                hits.Add((0, 36, 70));
                hits.Add((4, 38, 46));
                hits.Add((2, 42, 25));
                hits.Add((6, 42, 31));
                if (bar % 4 == 3) hits.Add((7, 38, 36));
                break;
            case "four-on-floor":
                for (var beat = 0; beat < BeatsPerBar; beat++) hits.Add((beat * 2, 36, beat % 2 == 0 ? 84 : 66));
                hits.Add((2, 38, 50));
                hits.Add((6, 38, 56));
                for (var step = 1; step < 8; step += 2) hits.Add((step, 42, step == 7 ? 35 : 28));
                break;
            case "swing":
            case "shuffle":
                hits.Add((0, 36, 70));
                hits.Add((2, 51, 34));
                hits.Add((4, 38, 54));
                hits.Add((6, 51, 40));
                if (bar % 4 == 3) hits.Add((7, 38, 38));
                break;
            case "broken-beat":
                hits.Add((0, 36, 74));
                hits.Add((2, 38, 54));
                hits.Add((3, 42, 25));
                hits.Add((5, 36, 58));
                hits.Add((6, 38, 48));
                hits.Add((7, 42, 30));
                break;
            case "clave":
                hits.Add((0, 36, 74));
                hits.Add((0, 75, 42));
                hits.Add((3, 75, 34));
                hits.Add((4, 38, 48));
                hits.Add((4, 75, 38));
                hits.Add((7, 75, 33));
                break;
            case "arpeggiated":
                if (bar % 4 == 3) hits.Add((6, 51, 24));
                break;
            default:
                hits.Add((0, 36, 78));
                hits.Add((4, 36, 58));
                hits.Add((2, 38, 62));
                hits.Add((6, 38, 66));
                for (var step = 0; step < 8; step += 2) hits.Add((step, 42, step is 0 or 4 ? 34 : 24));
                if (bar % 4 == 3) hits.Add((7, 38, 48));
                break;
        }

        if ((bar + (uint)seed) % 8 == 7 && groove is ("backbeat" or "four-on-floor" or "broken-beat"))
        {
            hits.Add((7, 38, 50));
        }

        return hits;
    }

    private static int SwingOffset(string groove, int step) =>
        groove is ("swing" or "shuffle") && step % 2 == 1 ? EighthNoteTicks / 3 : 0;

    private static double SectionIntensity(ArrangementSection section, long bar, long barCount, string density)
    {
        var sectionLength = Math.Max(1, section.EndBar - section.StartBar);
        var positionInSection = (bar - section.StartBar + 1) / (double)sectionLength;
        var entrance = section.Type == ArrangementSectionType.Intro ? 0.7 + positionInSection * 0.3 : 1.0;
        var exit = section.Type == ArrangementSectionType.Outro ? 1.0 - positionInSection * 0.48 : 1.0;
        var phraseBreath = barCount > 8 && bar % 8 == 7 ? 0.86 : 1.0;
        return Math.Clamp(section.Energy * entrance * exit * phraseBreath * ArrangementStructure.DensityMultiplier(density), 0.22, 1.08);
    }

    private static (int Chords, int Bass, int Lead) GetPrograms(ArrangementPlan plan)
    {
        var variant = plan.PaletteVariant % 3;
        return plan.InstrumentSet switch
        {
            "ambient" => variant switch
            {
                0 => (89, 38, 91),
                1 => (4, 32, 73),
                _ => (10, 39, 88)
            },
            "lofi" => variant switch
            {
                0 => (4, 33, 11),
                1 => (0, 32, 10),
                _ => (5, 33, 12)
            },
            "pop" => variant switch
            {
                0 => (4, 33, 80),
                1 => (0, 33, 11),
                _ => (89, 38, 80)
            },
            "electronic" => variant switch
            {
                0 => (88, 38, 81),
                1 => (90, 39, 80),
                _ => (81, 38, 87)
            },
            "cinematic" => variant switch
            {
                0 => (48, 43, 52),
                1 => (0, 43, 73),
                _ => (50, 48, 52)
            },
            "rock" => variant switch
            {
                0 => (27, 34, 29),
                1 => (25, 33, 30),
                _ => (28, 34, 27)
            },
            _ => variant switch
            {
                0 => (0, 32, 73),
                1 => (24, 32, 11),
                _ => (4, 32, 65)
            }
        };
    }

    private enum TrackRole
    {
        Chords,
        Bass,
        Lead,
        Drums
    }

    private static void WriteMixerControls(MidiTrackWriter track, byte channel, ArrangementPlan plan, TrackRole role)
    {
        var (volume, pan) = (plan.MixProfile, role) switch
        {
            ("spacious", TrackRole.Chords) => (70, 48),
            ("spacious", TrackRole.Bass) => (82, 64),
            ("spacious", TrackRole.Lead) => (72, 82),
            ("spacious", TrackRole.Drums) => (67, 62),
            ("intimate", TrackRole.Chords) => (72, 55),
            ("intimate", TrackRole.Bass) => (82, 64),
            ("intimate", TrackRole.Lead) => (76, 72),
            ("intimate", TrackRole.Drums) => (64, 64),
            ("warm", TrackRole.Chords) => (76, 54),
            ("warm", TrackRole.Bass) => (80, 64),
            ("warm", TrackRole.Lead) => (72, 74),
            ("warm", TrackRole.Drums) => (66, 62),
            ("balanced", TrackRole.Chords) => (72, 54),
            ("balanced", TrackRole.Bass) => (82, 64),
            ("balanced", TrackRole.Lead) => (74, 74),
            _ => (68, 64)
        };

        track.WriteAt(0, (byte)(0xB0 | channel), 7, (byte)volume);
        track.WriteAt(0, (byte)(0xB0 | channel), 10, (byte)pan);
    }

    private static IReadOnlyList<MidiControllerEvent> CreateExpressionEvents(
        byte channel,
        ArrangementPlan plan,
        IReadOnlyList<ArrangementSection> sections,
        long ticksPerBar,
        long barCount,
        TrackRole role)
    {
        var baseExpression = role switch
        {
            TrackRole.Chords => 94,
            TrackRole.Bass => 102,
            TrackRole.Lead => 96,
            _ => 90
        };
        var output = new List<MidiControllerEvent>();
        for (long bar = 0; bar < barCount; bar++)
        {
            var section = ArrangementStructure.At(sections, bar);
            var value = ScaleVelocity(baseExpression, SectionIntensity(section, bar, barCount, plan.TextureDensity));
            output.Add(new MidiControllerEvent(bar * ticksPerBar, channel, 11, value));
        }

        return output;
    }

    private static void WriteTextEvent(MidiTrackWriter track, long tick, string text)
    {
        var content = Encoding.UTF8.GetBytes(text);
        if (content.Length > 127)
        {
            content = content[..127];
        }

        track.WriteAt(tick, [0xFF, 0x06, (byte)content.Length, .. content]);
    }

    private static int ScaleVelocity(int velocity, double multiplier) =>
        Math.Clamp((int)Math.Round(velocity * multiplier), 1, 127);

    private static void AddNote(
        ICollection<MidiNoteEvent> events,
        int note,
        long start,
        long durationTicks,
        long targetTicks,
        int velocity)
    {
        if (start >= targetTicks || note is < 0 or > 127)
        {
            return;
        }

        var end = Math.Min(start + durationTicks, targetTicks);
        if (end <= start)
        {
            return;
        }

        events.Add(new MidiNoteEvent(start, IsOn: true, note, velocity));
        events.Add(new MidiNoteEvent(end, IsOn: false, note, Velocity: 0));
    }

    private static void WriteNotes(
        MidiTrackWriter track,
        byte channel,
        IEnumerable<MidiNoteEvent> events,
        IEnumerable<MidiControllerEvent> controllers)
    {
        var messages = events.Select(item => new MidiMessageEvent(
                item.Tick,
                item.IsOn ? 2 : 0,
                [(byte)((item.IsOn ? 0x90 : 0x80) | channel), (byte)item.Note, (byte)item.Velocity]))
            .Concat(controllers.Select(item => new MidiMessageEvent(
                item.Tick,
                1,
                [(byte)(0xB0 | item.Channel), (byte)item.Controller, (byte)item.Value])))
            .OrderBy(item => item.Tick)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Message[1]);

        foreach (var item in messages)
        {
            track.WriteAt(item.Tick, item.Message);
        }
    }

    private static void WriteTrack(BinaryWriter fileWriter, Action<MidiTrackWriter> writeEvents, long targetTicks)
    {
        using var trackData = new MemoryStream();
        using (var eventWriter = new BinaryWriter(trackData, Encoding.ASCII, leaveOpen: true))
        {
            var track = new MidiTrackWriter(eventWriter);
            writeEvents(track);
            track.EndAt(targetTicks);
        }

        if (trackData.Length > uint.MaxValue)
        {
            throw new InvalidDataException("MIDI track exceeds the Standard MIDI File size limit.");
        }

        WriteAscii(fileWriter, "MTrk");
        WriteUInt32BigEndian(fileWriter, (uint)trackData.Length);
        fileWriter.Write(trackData.GetBuffer(), 0, checked((int)trackData.Length));
    }

    private static void WriteAscii(BinaryWriter writer, string value) =>
        writer.Write(Encoding.ASCII.GetBytes(value));

    private static void WriteUInt16BigEndian(BinaryWriter writer, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, checked((ushort)value));
        writer.Write(bytes);
    }

    private static void WriteUInt32BigEndian(BinaryWriter writer, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        writer.Write(bytes);
    }

    private readonly record struct MidiNoteEvent(long Tick, bool IsOn, int Note, int Velocity);

    private readonly record struct MidiControllerEvent(long Tick, byte Channel, int Controller, int Value);

    private readonly record struct MidiMessageEvent(long Tick, int Priority, byte[] Message);

    private sealed class MidiTrackWriter(BinaryWriter writer)
    {
        private long _currentTick;

        public void WriteAt(long tick, params byte[] message)
        {
            if (tick < _currentTick)
            {
                throw new InvalidDataException("MIDI events must be written in chronological order.");
            }

            WriteVariableLength(writer, tick - _currentTick);
            writer.Write(message);
            _currentTick = tick;
        }

        public void EndAt(long targetTick) => WriteAt(targetTick, 0xFF, 0x2F, 0x00);

        private static void WriteVariableLength(BinaryWriter writer, long value)
        {
            if (value is < 0 or > 0x0FFF_FFFF)
            {
                throw new InvalidDataException("MIDI delta time is outside the Standard MIDI File range.");
            }

            var buffer = new byte[4];
            var index = 0;
            buffer[index++] = (byte)(value & 0x7F);
            value >>= 7;
            while (value > 0)
            {
                buffer[index++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }

            for (var position = index - 1; position >= 0; position--)
            {
                writer.Write(buffer[position]);
            }
        }
    }
}
