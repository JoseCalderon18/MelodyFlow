namespace MelodyFlow.Infrastructure.Storage;

/// <summary>Resuelve carpetas compartidas de medios y temporales para API y Worker.</summary>
public sealed class MediaStoragePaths
{
    public MediaStoragePaths(string? storageRoot, string? temporaryRoot = null)
    {
        var defaultRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MelodyFlow",
            "Media");
        var resolvedRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(storageRoot) ? defaultRoot : storageRoot);
        var parent = Directory.GetParent(resolvedRoot)?.FullName ?? resolvedRoot;
        var resolvedTemporaryRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(temporaryRoot) ? Path.Combine(parent, "Temp") : temporaryRoot);

        if (!Path.IsPathRooted(resolvedRoot) || !Path.IsPathRooted(resolvedTemporaryRoot))
        {
            throw new ArgumentException("Media and temporary storage roots must be absolute paths.");
        }

        StorageRoot = resolvedRoot;
        TemporaryRoot = resolvedTemporaryRoot;
    }

    public string StorageRoot { get; }
    public string TemporaryRoot { get; }
}
