using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public sealed class ChocolateyInstallService : IChocolateyInstallService
{
    private readonly IChocolateyExecutor _executor;

    public ChocolateyInstallService(IChocolateyExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public Task<ChocolateyRunResult> InstallAsync(string packageId, Action<string>? onLine, CancellationToken ct)
        => RunOperation("install", packageId, onLine, ct);

    public Task<ChocolateyRunResult> UninstallAsync(string packageId, Action<string>? onLine, CancellationToken ct)
        => RunOperation("uninstall", packageId, onLine, ct);

    public Task<ChocolateyRunResult> UpgradeAsync(string packageId, Action<string>? onLine, CancellationToken ct)
        => RunOperation("upgrade", packageId, onLine, ct);

    private Task<ChocolateyRunResult> RunOperation(
        string verb,
        string packageId,
        Action<string>? onLine,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("packageId obrigatório.", nameof(packageId));

        var args = new[]
        {
            verb,
            packageId,
            "-y",
            "--no-progress"
        };

        return _executor.RunAsync(args, onStdout: onLine, onStderr: onLine, ct: ct);
    }
}
