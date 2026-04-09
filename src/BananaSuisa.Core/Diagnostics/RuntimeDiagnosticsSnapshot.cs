using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Workspace;

namespace BananaSuisa.Core.Diagnostics;

public sealed record RuntimeDiagnosticsSnapshot(
    string AppVersion,
    string BaseDirectory,
    WorkspacePaths? WorkspacePaths,
    WorkspaceBootstrapResult? WorkspaceBootstrapResult,
    ConfigurationLoadResult? ConfigurationLoadResult,
    CatalogLoadResult? CatalogLoadResult,
    string? WingetPath,
    IReadOnlyList<DiagnosticCheck> Checks,
    DateTime GeneratedAtUtc);
