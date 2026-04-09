using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Workspace;

namespace BananaSuisa.Core.Diagnostics;

public sealed record RuntimeDiagnosticsSnapshot(
    string AppVersion,
    string BaseDirectory,
    WorkspacePaths? WorkspacePaths,
    WorkspaceBootstrapResult? WorkspaceBootstrapResult,
    ConfigurationLoadResult? ConfigurationLoadResult,
    string? WingetPath,
    IReadOnlyList<DiagnosticCheck> Checks,
    DateTime GeneratedAtUtc);
