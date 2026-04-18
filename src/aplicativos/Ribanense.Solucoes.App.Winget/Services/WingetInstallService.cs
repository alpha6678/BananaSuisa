using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public sealed class WingetInstallService : IWingetInstallService
{
    private readonly IWingetExecutor _executor;

    public WingetInstallService(IWingetExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public Task<WingetRunResult> InstallAsync(string packageId, Action<string>? onLine, CancellationToken ct)
        => RunOperation("install", packageId, onLine, ct, extraArgs:
            ["--silent", "--accept-package-agreements"]);

    public Task<WingetRunResult> UninstallAsync(string packageId, Action<string>? onLine, CancellationToken ct)
        => RunOperation("uninstall", packageId, onLine, ct, extraArgs:
            ["--silent"]);

    public Task<WingetRunResult> UpgradeAsync(string packageId, Action<string>? onLine, CancellationToken ct)
        => RunOperation("upgrade", packageId, onLine, ct, extraArgs:
            ["--silent", "--accept-package-agreements"]);

    private Task<WingetRunResult> RunOperation(
        string verb,
        string packageId,
        Action<string>? onLine,
        CancellationToken ct,
        string[] extraArgs)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            throw new ArgumentException("packageId obrigatório.", nameof(packageId));

        var args = new List<string>
        {
            verb,
            "--id", packageId,
            "--exact",
            "--disable-interactivity",
            "--accept-source-agreements"
        };
        args.AddRange(extraArgs);

        return _executor.RunAsync(args, onStdout: onLine, onStderr: onLine, ct: ct);
    }
}
