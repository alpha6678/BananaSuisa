using BananaSuisa.Core.Diagnostics;

namespace BananaSuisa.Services.Abstractions;

public interface IRuntimeDiagnosticsService
{
    RuntimeDiagnosticsSnapshot Collect(string startPath);
}
