namespace MelodyFlow.Application.Music;

/// <summary>
/// Guarda los archivos multimedia fuera de PostgreSQL y acepta claves relativas de la aplicación.
/// </summary>
public interface IArtifactStorage
{
    /// <summary>Guarda el contenido bajo una clave relativa gestionada por la implementación.</summary>
    Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken);

    /// <summary>Abre el archivo para lectura o devuelve null si no existe.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Comprueba si existe un archivo asociado a la clave.</summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken);
}
