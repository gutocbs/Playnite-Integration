namespace Launcher.Host;

public static class TemporaryTransportCleaner
{
    public static Task CleanupAsync(
        string transportRoot,
        string currentExecutionDirectory,
        TimeSpan maximumAge,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => Cleanup(transportRoot, currentExecutionDirectory, maximumAge, cancellationToken),
            cancellationToken);

    private static void Cleanup(
        string transportRoot,
        string currentExecutionDirectory,
        TimeSpan maximumAge,
        CancellationToken cancellationToken)
    {
        if (maximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge));
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(transportRoot));
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentExecutionDirectory));
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
                if (string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetDirectoryName(candidate), root, StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParse(Path.GetFileName(candidate), out _))
                {
                    continue;
                }

                var lastWriteTime = Directory.GetLastWriteTimeUtc(candidate);
                foreach (var file in Directory.EnumerateFiles(candidate, "*", SearchOption.AllDirectories))
                {
                    var fileLastWriteTime = File.GetLastWriteTimeUtc(file);
                    if (fileLastWriteTime > lastWriteTime)
                    {
                        lastWriteTime = fileLastWriteTime;
                    }
                }

                if (DateTime.UtcNow - lastWriteTime > maximumAge)
                {
                    Directory.Delete(candidate, recursive: true);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Stale transport cleanup is best effort and must never block launching.
            }
        }
    }
}
