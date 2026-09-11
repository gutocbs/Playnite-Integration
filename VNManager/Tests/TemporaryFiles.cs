namespace Launcher.Tests;

internal sealed class TemporaryFiles : IDisposable
{
    public TemporaryFiles()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"vnmanager-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public string CreateFile(string fileName, string contents = "")
    {
        var path = Path.Combine(DirectoryPath, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch
        {
        }
    }
}
