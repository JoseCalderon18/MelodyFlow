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

/// <summary>
/// Escribe un archivo MIDI estándar directamente: el resultado es determinista y no necesita un DAW.
/// </summary>
internal sealed class MidiComposer : IMidiComposer
{
    private const int TicksPerQuarterNote = 480;
    private const int BeatsPerBar = 4;

    public async Task ComposeAsync(
        ArrangementPlan plan,
        int targetDurationSeconds,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (targetDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetDurationSeconds));
        }

        if (!MusicKey.TryParse(plan.Key, out var key))
        {
            throw new InvalidDataException("The arrangement key is not supported.");
        }

        // Convierte la duración del proyecto a ticks MIDI y redondea hacia arriba para cubrirla completa.
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
            WriteUInt16BigEndian(writer, 1); // Formato 1: una pista de tempo y pistas separadas para instrumentos.
            WriteUInt16BigEndian(writer, 4);
            WriteUInt16BigEndian(writer, TicksPerQuarterNote);

            // El formato 1 separa tempo, acordes, bajo y melodía en pistas independientes.
            WriteTrack(writer, track => WriteConductorTrack(track, microsecondsPerQuarter), targetTicks);
            WriteTrack(writer, track => WriteChordTrack(track, plan, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteBassTrack(track, plan, barCount, ticksPerBar, targetTicks), targetTicks);
            WriteTrack(writer, track => WriteMelodyTrack(track, plan, key, barCount, ticksPerBar, targetTicks), targetTicks);
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
        track.WriteAt(0, (byte)(0xC0 | channel), 4); // Piano eléctrico.

        for (long bar = 0; bar < barCount; bar++)
        {
            var start = bar * ticksPerBar;
            if (start >= targetTicks)
            {
                break;
            }

            Chord.TryParse(plan.Chords[(int)(bar % plan.Chords.Count)], out var chord);
            var notes = chord.Intervals.Select(interval => 60 + chord.RootPitchClass + interval).ToArray();
            var noteOff = Math.Min(start + (TicksPerQuarterNote * 3L), targetTicks);
            foreach (var note in notes)
            {
                track.WriteAt(start, (byte)(0x90 | channel), (byte)note, 76);
            }
            foreach (var note in notes)
            {
                track.WriteAt(noteOff, (byte)(0x80 | channel), (byte)note, 0);
            }
        }
    }

    private static void WriteBassTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 1;
        track.WriteAt(0, (byte)(0xC0 | channel), 32); // Bajo acústico.

        for (long bar = 0; bar < barCount; bar++)
        {
            var start = bar * ticksPerBar;
            if (start >= targetTicks)
            {
                break;
            }

            Chord.TryParse(plan.Chords[(int)(bar % plan.Chords.Count)], out var chord);
            var root = 36 + chord.RootPitchClass;
            WriteBassNote(track, channel, root, start, targetTicks);
            WriteBassNote(track, channel, root - 12, start + (TicksPerQuarterNote * 2L), targetTicks);
        }
    }

    private static void WriteBassNote(MidiTrackWriter track, byte channel, int note, long start, long targetTicks)
    {
        if (start >= targetTicks)
        {
            return;
        }

        track.WriteAt(start, (byte)(0x90 | channel), (byte)note, 82);
        track.WriteAt(Math.Min(start + TicksPerQuarterNote, targetTicks), (byte)(0x80 | channel), (byte)note, 0);
    }

    private static void WriteMelodyTrack(
        MidiTrackWriter track,
        ArrangementPlan plan,
        MusicKey key,
        long barCount,
        long ticksPerBar,
        long targetTicks)
    {
        const byte channel = 2;
        track.WriteAt(0, (byte)(0xC0 | channel), 10); // Caja musical.
        // Misma semilla y mismo plan producen las mismas notas de melodía.
        var random = new Random(plan.VariationSeed);

        for (long bar = 0; bar < barCount; bar++)
        {
            var barStart = bar * ticksPerBar;
            for (var beat = 0; beat < BeatsPerBar; beat++)
            {
                var start = barStart + (beat * (long)TicksPerQuarterNote);
                if (start >= targetTicks)
                {
                    return;
                }

                var note = key.GetScaleNote(random.Next(0, 14), octaveOffset: 0);
                var velocity = random.Next(62, 90);
                track.WriteAt(start, (byte)(0x90 | channel), (byte)note, (byte)velocity);
                track.WriteAt(Math.Min(start + (TicksPerQuarterNote / 2L), targetTicks), (byte)(0x80 | channel), (byte)note, 0);
            }
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

            // MIDI codifica tiempos delta en grupos de 7 bits; el bit alto indica que continúa otro grupo.
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
