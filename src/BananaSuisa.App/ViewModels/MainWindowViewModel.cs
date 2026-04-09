using BananaSuisa.Core.Diagnostics;

namespace BananaSuisa.App.ViewModels;

public sealed class MainWindowViewModel
{
    private MainWindowViewModel(
        string title,
        string subtitle,
        string baseDirectory,
        string projectRoot,
        string resourcesRoot,
        string memoryRoot,
        string wingetPath,
        string generatedAt,
        IReadOnlyList<DiagnosticCheckViewModel> checks)
    {
        Title = title;
        Subtitle = subtitle;
        BaseDirectory = baseDirectory;
        ProjectRoot = projectRoot;
        ResourcesRoot = resourcesRoot;
        MemoryRoot = memoryRoot;
        WingetPath = wingetPath;
        GeneratedAt = generatedAt;
        Checks = checks;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string BaseDirectory { get; }

    public string ProjectRoot { get; }

    public string ResourcesRoot { get; }

    public string MemoryRoot { get; }

    public string WingetPath { get; }

    public string GeneratedAt { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> Checks { get; }

    public static MainWindowViewModel FromSnapshot(RuntimeDiagnosticsSnapshot snapshot)
    {
        IReadOnlyList<DiagnosticCheckViewModel> checks = snapshot.Checks
            .Select(check => new DiagnosticCheckViewModel(check.Name, check.IsHealthy, check.Detail))
            .ToArray();

        string projectRoot = snapshot.WorkspacePaths?.ProjectRoot ?? "Nao localizado";
        string resourcesRoot = snapshot.WorkspacePaths?.ResourcesRoot ?? "Nao localizado";
        string memoryRoot = snapshot.WorkspacePaths?.MemoryRoot ?? "Nao localizado";

        return new MainWindowViewModel(
            title: $"BananaSuisa .NET Bootstrap v{snapshot.AppVersion}",
            subtitle: "Primeiro esqueleto WPF com diagnostico de runtime, paths e winget.",
            baseDirectory: snapshot.BaseDirectory,
            projectRoot: projectRoot,
            resourcesRoot: resourcesRoot,
            memoryRoot: memoryRoot,
            wingetPath: snapshot.WingetPath ?? "Nao encontrado",
            generatedAt: snapshot.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
            checks: checks);
    }
}
