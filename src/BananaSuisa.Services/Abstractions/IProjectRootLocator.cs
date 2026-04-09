namespace BananaSuisa.Services.Abstractions;

public interface IProjectRootLocator
{
    string? TryLocateFrom(string startPath);
}
