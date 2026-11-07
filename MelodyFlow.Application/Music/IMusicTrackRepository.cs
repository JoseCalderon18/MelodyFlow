using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Music;

/// <summary>Define las consultas y la persistencia idempotente de metadatos de audio.</summary>
public interface IMusicTrackRepository
{
    /// <summary>Busca una canción por su clave primaria.</summary>
    Task<MusicTrack?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca la canción producida por un trabajo concreto.</summary>
    Task<MusicTrack?> GetByGenerationJobIdAsync(Guid generationJobId, CancellationToken cancellationToken);

    /// <summary>
    /// Evita duplicar el resultado si el Worker reintenta después de guardar el archivo.
    /// </summary>
    Task<MusicTrack> AddOrGetByGenerationJobIdAsync(MusicTrack track, CancellationToken cancellationToken);
}
