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

            WriteTrack(writer, track => WriteConductorTrack(track, microsecondsPerQuarter), targetTicks);
            WriteTrack(writer, track => WriteChordTrack(track, plan, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteBassTrack(track, plan, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteMelodyTrack(track, plan, key, scale, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteDrumTrack(track, plan, barCount, ticksPerBar, targetTicks), targetTicks);
        }

        await stream.FlushAsync(cancellationToken);
    }

    private static void WriteConductorTrack(MidiTrackWriter track, int microsecondsPerQuarter)
    {
        track.WriteAt(0, 0xFF, 0x51, 0x03,
            (byte)(microsecondsPerQuarter >> 16),
            (byte)(microsecondsPerQuarter >> 8),
            (byte)microsecondsPerQuarter);
        track.WriteAt(0, 0xFF, 0x58, 0x04, BeatsPerBar, 0x02, 24, 8);
    }

    private static void WriteChordTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 0;
        track.WriteAt(0, (byte)(0xC0 | channel), (byte)ChordProgram(plan.InstrumentSet));
        var events = new List<MidiNoteEvent>();

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            if (!Chord.TryParse(plan.Chords[(int)(bar % plan.Chords.Count)], out var chord))
            {
                throw new InvalidDataException("The arrangement contains an unsupported chord.");
            }

            var voicing = BuildVoicing(chord, bar, plan.VariationSeed);
            var intensity = SectionIntensity(bar, barCount);
            foreach (var (offset, duration, velocityScale) in ChordRhythm(plan.Groove, bar, plan.VariationSeed))
            {
                var velocity = ScaleVelocity(72, intensity * velocityScale);
                foreach (var note in voicing)
                {
                    AddNote(events, note, barStart + offset, duration, targetTicks, velocity);
                }
            }
        }

        WriteNotes(track, channel, events);
    }

    private static void WriteBassTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 1;
        track.WriteAt(0, (byte)(0xC0 | channel), (byte)BassProgram(plan.InstrumentSet));
        var events = new List<MidiNoteEvent>();

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            if (!Chord.TryParse(plan.Chords[(int)(bar % plan.Chords.Count)], out var chord))
            {
                throw new InvalidDataException("The arrangement contains an unsupported chord.");
            }

            var intensity = SectionIntensity(bar, barCount);
            foreach (var (beat, degree, durationBeats, velocityScale) in BassRhythm(plan.Groove, bar, plan.VariationSeed))
            {
                var note = 36 + chord.RootPitchClass + degree;
                var velocity = ScaleVelocity(78, intensity * velocityScale);
                AddNote(events, note, barStart + (beat * TicksPerQuarterNote), durationBeats * TicksPerQuarterNote,
                    targetTicks, velocity);
            }
        }

        WriteNotes(track, channel, events);
    }

    private static void WriteMelodyTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        MusicKey key,
        MusicalScaleDefinition scale,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 2;
        track.WriteAt(0, (byte)(0xC0 | channel), (byte)LeadProgram(plan.InstrumentSet));
        var events = new List<MidiNoteEvent>();
        var random = new Random(plan.VariationSeed ^ 0x2F10A9);

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            var rhythm = MelodySteps(plan.Groove, bar, plan.VariationSeed);
            var motifOffset = (int)((bar / 2 + (uint)plan.VariationSeed) % 7);
            var intensity = SectionIntensity(bar, barCount);
            foreach (var step in rhythm)
            {
                if (step == 7 && random.Next(0, 4) == 0)
                {
                    continue;
                }

                var degree = MelodyDegree(step, bar, motifOffset, random);
                var octave = plan.InstrumentSet is "cinematic" or "ambient" ? 1 : 0;
                var note = scale.GetMidiNote(key.RootPitchClass, degree, octaveOffset: octave);
                var velocity = ScaleVelocity(68 + random.Next(0, 18), intensity);
                var start = barStart + (step * EighthNoteTicks);
                var noteLength = plan.Groove == "ambient" ? EighthNoteTicks * 3L : EighthNoteTicks - 36L;
                AddNote(events, note, start, noteLength, targetTicks, velocity);
            }
        }

        WriteNotes(track, channel, events);
    }

    private static void WriteDrumTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 9;
        var events = new List<MidiNoteEvent>();
        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            if (barStart >= targetTicks)
            {
                break;
            }

            var intensity = SectionIntensity(bar, barCount);
            foreach (var (step, note, velocity) in DrumHits(plan.Groove, bar, plan.VariationSeed))
            {
                AddNote(events, note, barStart + (step * EighthNoteTicks), durationTicks: 60,
                    targetTicks, ScaleVelocity(velocity, intensity));
            }
        }

        WriteNotes(track, channel, events);
    }

    private static IReadOnlyList<int> BuildVoicing(Chord chord, long bar, int seed)
    {
        var notes = chord.Intervals.Select(interval => 60 + chord.RootPitchClass + interval).ToArray();
        var inversion = (int)((bar + (uint)seed) % notes.Length);
        return notes.Skip(inversion)
            .Concat(notes.Take(inversion).Select(note => note + 12))
            .Order()
            .ToArray();
    }

    private static IEnumerable<(long Offset, long Duration, double VelocityScale)> ChordRhythm(
        string groove,
        long bar,
        int seed)
    {
        return groove switch
        {
            "ambient" => [(0, TicksPerQuarterNote * 4L - 24, 0.78)],
            "cinematic" => [(0, TicksPerQuarterNote * 4L - 24, bar % 8 == 7 ? 1.0 : 0.88)],
            "lofi" => [(0, TicksPerQuarterNote * 2L - 30, 1.0),
                (TicksPerQuarterNote * 2L + (seed % 2 == 0 ? 0 : EighthNoteTicks), TicksPerQuarterNote + 90, 0.78)],
            "four-on-floor" =>
            [
                (EighthNoteTicks, EighthNoteTicks, 0.72),
                (TicksPerQuarterNote + EighthNoteTicks, EighthNoteTicks, 0.62),
                (TicksPerQuarterNote * 2L + EighthNoteTicks, EighthNoteTicks, 0.72),
                (TicksPerQuarterNote * 3L + EighthNoteTicks, EighthNoteTicks, 0.62)
            ],
            _ => [(0, TicksPerQuarterNote * 2L - 24, 1.0),
                (TicksPerQuarterNote * 2L, TicksPerQuarterNote - 30, 0.74)]
        };
    }

    private static IEnumerable<(int Beat, int Degree, int DurationBeats, double VelocityScale)> BassRhythm(
        string groove,
        long bar,
        int seed)
    {
        if (groove == "ambient")
        {
            return [(0, 0, 4, 0.64)];
        }

        if (groove == "cinematic")
        {
            return bar % 2 == 0
                ? [(0, 0, 4, 1.0)]
                : [(0, 0, 2, 0.9), (2, 4, 2, 0.72)];
        }

        if (groove == "four-on-floor")
        {
            return [(0, 0, 1, 1.0), (1, 0, 1, 0.78), (2, 0, 1, 1.0), (3, bar % 2 == 0 ? 0 : 4, 1, 0.82)];
        }

        if (groove == "lofi")
        {
            return seed % 2 == 0
                ? [(0, 0, 2, 1.0), (2, 4, 1, 0.78), (3, 0, 1, 0.68)]
                : [(0, 0, 2, 1.0), (2, 4, 2, 0.78)];
        }

        return [(0, 0, 1, 1.0), (2, 4, 1, 0.78)];
    }

    private static IReadOnlyList<int> MelodySteps(string groove, long bar, int seed)
    {
        var pattern = groove switch
        {
            "ambient" => new[] { 0, 3, 6 },
            "lofi" => new[] { 0, 3, 4, 6 },
            "four-on-floor" => new[] { 1, 3, 5, 7 },
            "cinematic" => bar % 2 == 0 ? new[] { 0, 4, 6 } : new[] { 2, 4, 7 },
            _ => new[] { 0, 2, 4, 7 }
        };
        var shift = (int)((bar + (uint)seed) % pattern.Length);
        return pattern.Skip(shift).Concat(pattern.Take(shift)).ToArray();
    }

    private static int MelodyDegree(int step, long bar, int motifOffset, Random random)
    {
        int[] motif = (bar % 4) switch
        {
            0 => [0, 2, 4, 2, 1, 3, 4, 6],
            1 => [4, 3, 2, 0, 2, 1, 0, 1],
            2 => [2, 4, 5, 4, 2, 0, 1, 2],
            _ => [0, 1, 3, 2, 4, 3, 1, 0]
        };

        var degree = motif[step] + motifOffset;
        if (random.Next(0, 7) == 0)
        {
            degree += random.Next(0, 2) == 0 ? -1 : 1;
        }

        return degree;
    }

    private static IReadOnlyList<(int Step, int Note, int Velocity)> DrumHits(string groove, long bar, int seed)
    {
        var hits = new List<(int Step, int Note, int Velocity)>();
        switch (groove)
        {
            case "ambient":
                if (bar % 4 == 3)
                {
                    hits.Add((0, 49, 34));
                }
                break;
            case "cinematic":
                if (bar % 2 == 0) hits.Add((0, 35, 62));
                if (bar % 4 == 3) hits.Add((0, 49, 55));
                if (bar % 8 == 7) hits.Add((6, 38, 46));
                break;
            case "lofi":
                hits.Add((0, 36, 74));
                hits.Add((4, 38, 48));
                hits.Add((2, 42, 28));
                hits.Add((6, 42, 34));
                if (bar % 4 == 3) hits.Add((7, 38, 40));
                break;
            case "four-on-floor":
                for (var beat = 0; beat < BeatsPerBar; beat++) hits.Add((beat * 2, 36, beat % 2 == 0 ? 88 : 70));
                hits.Add((2, 38, 54));
                hits.Add((6, 38, 60));
                for (var step = 1; step < 8; step += 2) hits.Add((step, 42, step == 7 ? 38 : 30));
                break;
            default:
                hits.Add((0, 36, 82));
                hits.Add((4, 36, 64));
                hits.Add((2, 38, 68));
                hits.Add((6, 38, 72));
                for (var step = 0; step < 8; step += 2) hits.Add((step, 42, step is 0 or 4 ? 36 : 26));
                if (bar % 4 == 3) hits.Add((7, 38, 54));
                break;
        }

        if ((bar + (uint)seed) % 8 == 7 && groove is "backbeat" or "four-on-floor")
        {
            hits.Add((7, 38, 56));
        }

        return hits;
    }

    private static double SectionIntensity(long bar, long barCount)
    {
        var position = bar / (double)Math.Max(1, barCount);
        return position switch
        {
            < 0.12 => 0.55,
            < 0.27 => 0.76,
            < 0.70 => 1.0,
            < 0.84 => 0.62,
            _ => 0.84
        };
    }

    private static int ChordProgram(string instrumentSet) => instrumentSet switch
    {
        "ambient" => 89,
        "lofi" => 4,
        "pop" => 4,
        "electronic" => 88,
        "cinematic" => 48,
        "rock" => 27,
        _ => 0
    };

    private static int BassProgram(string instrumentSet) => instrumentSet switch
    {
        "ambient" => 38,
        "lofi" => 33,
        "pop" => 33,
        "electronic" => 38,
        "cinematic" => 43,
        "rock" => 34,
        _ => 32
    };

    private static int LeadProgram(string instrumentSet) => instrumentSet switch
    {
        "ambient" => 91,
        "lofi" => 11,
        "pop" => 80,
        "electronic" => 81,
        "cinematic" => 52,
        "rock" => 27,
        _ => 10
    };

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

    private static void WriteNotes(MidiTrackWriter track, byte channel, IEnumerable<MidiNoteEvent> events)
    {
        foreach (var item in events.OrderBy(item => item.Tick)
                     .ThenBy(item => item.IsOn ? 1 : 0)
                     .ThenBy(item => item.Note))
        {
            track.WriteAt(item.Tick,
                (byte)((item.IsOn ? 0x90 : 0x80) | channel),
                (byte)item.Note,
                (byte)item.Velocity);
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
