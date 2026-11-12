using MelodyFlow.Domain.Entities;
using MelodyFlow.Application.Media;

namespace MelodyFlow.Application.Music;

/// <summary>Define las consultas y la persistencia idempotente de metadatos de audio.</summary>
public interface IMusicTrackRepository
{
    /// <summary>Busca una canción por su clave primaria.</summary>
    Task<MusicTrack?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca la canción producida por un trabajo concreto.</summary>
    Task<MusicTrack?> GetByGenerationJobIdAsync(Guid generationJobId, CancellationToken cancellationToken);

    /// <summary>Devuelve una página de canciones recientes para la biblioteca del usuario.</summary>
    Task<MusicTrackPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Deletes a track only when no rendered video references it.</summary>
    Task<DeleteResourceResult> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Evita duplicar el resultado si el Worker reintenta después de guardar el archivo.
    /// </summary>
    Task<MusicTrack> AddOrGetByGenerationJobIdAsync(MusicTrack track, CancellationToken cancellationToken);
}

/// <summary>Una página de canciones junto con los datos necesarios para navegar el listado.</summary>
public sealed record MusicTrackPage(IReadOnlyList<MusicTrack> Items, int Page, int PageSize, int TotalCount);
