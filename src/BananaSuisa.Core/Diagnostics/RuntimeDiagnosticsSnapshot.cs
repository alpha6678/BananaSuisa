using BananaSuisa.Core.Workspace;

namespace BananaSuisa.Core.Diagnostics;

public sealed record RuntimeDiagnosticsSnapshot(
    string AppVersion,
    string BaseDirectory,
    WorkspacePaths? WorkspacePaths,
    WorkspaceBootstrapResult? WorkspaceBootstrapResult,
    string? WingetPath,
    IReadOnlyList<DiagnosticCheck> Checks,
    DateTime GeneratedAtUtc);
