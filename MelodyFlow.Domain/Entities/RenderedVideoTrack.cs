namespace MelodyFlow.Domain.Entities;

/// <summary>Una canción y su posición en la secuencia musical de un videoclip.</summary>
public sealed class RenderedVideoTrack
{
    private RenderedVideoTrack()
    {
    }

    public RenderedVideoTrack(Guid renderedVideoId, Guid musicTrackId, int sequenceNumber)
    {
        if (renderedVideoId == Guid.Empty || musicTrackId == Guid.Empty)
        {
            throw new ArgumentException("A rendered video and music track are required.");
        }

        if (sequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber));
        }

        Id = Guid.NewGuid();
        RenderedVideoId = renderedVideoId;
        MusicTrackId = musicTrackId;
        SequenceNumber = sequenceNumber;
    }

    public Guid Id { get; private set; }
    public Guid RenderedVideoId { get; private set; }
    public Guid MusicTrackId { get; private set; }
    public int SequenceNumber { get; private set; }
}
