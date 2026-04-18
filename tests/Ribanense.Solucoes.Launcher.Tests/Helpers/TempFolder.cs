namespace Ribanense.Solucoes.Launcher.Tests.Helpers;

public sealed class TempFolder : IDisposable
{
    public string Path { get; }

    public TempFolder(string prefix = "ribanense-launcher-tests")
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            prefix,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Sub(params string[] segments)
    {
        string full = segments.Length == 0 ? Path : System.IO.Path.Combine(Path, System.IO.Path.Combine(segments));
        Directory.CreateDirectory(full);
        return full;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
