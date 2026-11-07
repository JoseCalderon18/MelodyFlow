using MelodyFlow.Domain.Entities;

namespace MelodyFlow.Application.Music;

/// <summary>
/// Puerto de Application para generar una canción sin depender de LM Studio ni de herramientas locales.
/// </summary>
public interface IMusicGenerationPipeline
{
    /// <summary>
    /// Genera el audio y devuelve sus metadatos; el callback comunica el avance al trabajo en cola.
    /// </summary>
    Task<MusicTrack> GenerateAsync(
        Guid generationJobId,
        MusicProject project,
        string requestedParametersJson,
        Func<int, CancellationToken, Task> reportProgressAsync,
        CancellationToken cancellationToken);
}
