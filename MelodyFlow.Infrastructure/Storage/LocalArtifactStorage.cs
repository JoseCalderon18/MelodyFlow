using MelodyFlow.Application.Music;

namespace MelodyFlow.Infrastructure.Storage;

/// <summary>
/// Escribe el archivo generado de forma atómica; PostgreSQL conserva únicamente su clave relativa.
/// </summary>
public sealed class LocalArtifactStorage : IArtifactStorage
{
    private readonly string _rootDirectory;

    public LocalArtifactStorage(string? rootDirectory = null)
    {
        // API y Worker deben resolver la misma carpeta para que la API pueda entregar el archivo.
        var configuredRoot = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MelodyFlow",
                "Media")
            : rootDirectory;

        if (!Path.IsPathRooted(configuredRoot))
        {
            throw new ArgumentException(
                "Media storage root must be an absolute path so API and Worker resolve the same folder.",
                nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(configuredRoot);
    }

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var destinationPath = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The storage key does not resolve to a directory.");
        Directory.CreateDirectory(directory);

        // El temporal comparte carpeta con el destino para que el cambio de nombre sea atómico en el mismo volumen.
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolvePath(storageKey)));
    }

    private string ResolvePath(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var segments = storageKey.Split('/');
        if (storageKey.StartsWith('/') ||
            storageKey.Contains('\\') ||
            storageKey.Contains(':') ||
            segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Storage key must be a safe relative path.", nameof(storageKey));
        }

        // Tras validar los segmentos, se comprueba también la ruta normalizada contra la carpeta raíz.
        var candidate = Path.GetFullPath(Path.Combine(
            [_rootDirectory, .. segments]));
        var rootWithSeparator = _rootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _rootDirectory
            : _rootDirectory + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!candidate.StartsWith(rootWithSeparator, comparison))
        {
            throw new ArgumentException("Storage key escapes the configured media directory.", nameof(storageKey));
        }

        return candidate;
    }
}
