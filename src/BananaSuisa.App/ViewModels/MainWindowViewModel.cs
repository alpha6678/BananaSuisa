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
        string dataRoot,
        string configPath,
        string wingetPath,
        string workspaceSummary,
        IReadOnlyList<DiagnosticCheckViewModel> workspaceItems,
        string generatedAt,
        IReadOnlyList<DiagnosticCheckViewModel> checks)
    {
        Title = title;
        Subtitle = subtitle;
        BaseDirectory = baseDirectory;
        ProjectRoot = projectRoot;
        ResourcesRoot = resourcesRoot;
        MemoryRoot = memoryRoot;
        DataRoot = dataRoot;
        ConfigPath = configPath;
        WingetPath = wingetPath;
        WorkspaceSummary = workspaceSummary;
        WorkspaceItems = workspaceItems;
        GeneratedAt = generatedAt;
        Checks = checks;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string BaseDirectory { get; }

    public string ProjectRoot { get; }

    public string ResourcesRoot { get; }

    public string MemoryRoot { get; }

    public string DataRoot { get; }

    public string ConfigPath { get; }

    public string WingetPath { get; }

    public string WorkspaceSummary { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> WorkspaceItems { get; }

    public string GeneratedAt { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> Checks { get; }

    public static MainWindowViewModel FromSnapshot(RuntimeDiagnosticsSnapshot snapshot)
    {
        IReadOnlyList<DiagnosticCheckViewModel> checks = snapshot.Checks
            .Select(check => new DiagnosticCheckViewModel(check.Name, check.IsHealthy, check.Detail))
            .ToArray();
        IReadOnlyList<DiagnosticCheckViewModel> workspaceItems = snapshot.WorkspaceBootstrapResult?.Items
            .Select(item => new DiagnosticCheckViewModel(item.Name, item.IsHealthy, item.Detail))
            .ToArray()
            ?? [];

        string projectRoot = snapshot.WorkspacePaths?.ProjectRoot ?? "Nao localizado";
        string resourcesRoot = snapshot.WorkspacePaths?.ResourcesRoot ?? "Nao localizado";
        string memoryRoot = snapshot.WorkspacePaths?.MemoryRoot ?? "Nao localizado";
        string dataRoot = snapshot.WorkspaceBootstrapResult?.Paths.DataRoot ?? "Nao localizado";
        string configPath = snapshot.WorkspaceBootstrapResult?.Paths.ConfigPath ?? "Nao localizado";
        string workspaceSummary = snapshot.WorkspaceBootstrapResult is null
            ? "Workspace nao inicializado."
            : $"Pastas criadas: {snapshot.WorkspaceBootstrapResult.CreatedDirectoryCount} | Arquivos sincronizados: {snapshot.WorkspaceBootstrapResult.SynchronizedFileCount}";

        return new MainWindowViewModel(
            title: $"BananaSuisa .NET Bootstrap v{snapshot.AppVersion}",
            subtitle: "Primeiro esqueleto WPF com diagnostico de runtime, winget e inicializacao de workspace.",
            baseDirectory: snapshot.BaseDirectory,
            projectRoot: projectRoot,
            resourcesRoot: resourcesRoot,
            memoryRoot: memoryRoot,
            dataRoot: dataRoot,
            configPath: configPath,
            wingetPath: snapshot.WingetPath ?? "Nao encontrado",
            workspaceSummary: workspaceSummary,
            workspaceItems: workspaceItems,
            generatedAt: snapshot.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
            checks: checks);
    }
}
