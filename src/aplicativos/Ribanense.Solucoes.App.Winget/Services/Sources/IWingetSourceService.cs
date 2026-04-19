using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services.Sources;

public interface IWingetSourceService
{
    Task<IReadOnlyList<WingetSource>> ListAsync(CancellationToken ct);

    Task<WingetRunResult> UpdateAsync(string? name, Action<string>? onLine, CancellationToken ct);

    Task<WingetRunResult> AddAsync(string name, string argument, string type, Action<string>? onLine, CancellationToken ct);

    Task<WingetRunResult> RemoveAsync(string name, Action<string>? onLine, CancellationToken ct);

    /// <summary>
    /// Reset requer elevacao; delega ao <see cref="Diagnostics.IElevatedCommandRunner"/>.
    /// </summary>
    Task<WingetRunResult> ResetAsync(IProgress<string>? onLine, CancellationToken ct);

    Task<string> ExportAsync(CancellationToken ct);
}
