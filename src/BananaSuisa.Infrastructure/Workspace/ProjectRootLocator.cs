using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Workspace;

public sealed class ProjectRootLocator : IProjectRootLocator
{
    private static readonly string[] Markers =
    [
        "BananaSuisa_recursos",
        "BananaSuisa_desenvolvimento"
    ];

    public string? TryLocateFrom(string startPath)
    {
        DirectoryInfo? currentDirectory = ResolveDirectory(startPath);

        while (currentDirectory is not null)
        {
            if (Markers.Any(marker => Directory.Exists(Path.Combine(currentDirectory.FullName, marker))))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static DirectoryInfo? ResolveDirectory(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        if (Directory.Exists(startPath))
        {
            return new DirectoryInfo(startPath);
        }

        if (File.Exists(startPath))
        {
            return new FileInfo(startPath).Directory;
        }

        return new DirectoryInfo(startPath);
    }
}
