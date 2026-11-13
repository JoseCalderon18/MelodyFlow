using System.Buffers.Binary;
using System.Text;

namespace MelodyFlow.Infrastructure.MusicGeneration;

/// <summary>Splits the composer's known five-track SMF into conductor-plus-part MIDI files.</summary>
internal static class MidiStemExporter
{
    private static readonly string[] Roles = ["chords", "bass", "lead", "drums"];

    public static async Task<IReadOnlyList<MidiStemSource>> ExportAsync(
        string arrangementPath,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(arrangementPath, cancellationToken);
        if (bytes.Length < 14 || !HasChunk(bytes, 0, "MThd") ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4)) != 6 ||
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2)) != 1 ||
            BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10, 2)) != 5)
        {
            throw new InvalidDataException("The composer produced an unsupported MIDI arrangement layout.");
        }

        var tracks = new List<ReadOnlyMemory<byte>>(5);
        var offset = 14;
        for (var index = 0; index < 5; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasChunk(bytes, offset, "MTrk") || bytes.Length - offset < 8)
            {
                throw new InvalidDataException("The composed MIDI file has an incomplete track chunk.");
            }

            var dataLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 4, 4));
            var chunkLength = checked(8L + dataLength);
            if (chunkLength > bytes.Length - offset)
            {
                throw new InvalidDataException("The composed MIDI file has an invalid track length.");
            }

            tracks.Add(bytes.AsMemory(offset, checked((int)chunkLength)));
            offset += checked((int)chunkLength);
        }

        if (offset != bytes.Length)
        {
            throw new InvalidDataException("The composed MIDI file contains unexpected trailing data.");
        }

        var directory = Path.GetDirectoryName(arrangementPath)
            ?? throw new InvalidDataException("The composed MIDI file has no output directory.");
        var stems = new List<MidiStemSource>(Roles.Length);
        for (var index = 0; index < Roles.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stemPath = Path.Combine(directory, $"stem-{Roles[index]}.mid");
            var stemBytes = new byte[14 + tracks[0].Length + tracks[index + 1].Length];
            bytes.AsSpan(0, 14).CopyTo(stemBytes);
            BinaryPrimitives.WriteUInt16BigEndian(stemBytes.AsSpan(10, 2), 2);
            tracks[0].Span.CopyTo(stemBytes.AsSpan(14));
            tracks[index + 1].Span.CopyTo(stemBytes.AsSpan(14 + tracks[0].Length));
            await File.WriteAllBytesAsync(stemPath, stemBytes, cancellationToken);
            stems.Add(new MidiStemSource(Roles[index], stemPath));
        }

        return stems;
    }

    private static bool HasChunk(ReadOnlySpan<byte> bytes, int offset, string id) =>
        offset >= 0 && bytes.Length - offset >= 4 &&
        bytes.Slice(offset, 4).SequenceEqual(Encoding.ASCII.GetBytes(id));
}
