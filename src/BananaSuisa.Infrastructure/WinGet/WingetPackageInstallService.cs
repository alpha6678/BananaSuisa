using BananaSuisa.Core.Winget;
using BananaSuisa.Infrastructure.Provisioning;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.WinGet;

public sealed class WingetPackageInstallService : IWingetPackageInstallService
{
    private readonly IWingetLocator _locator;

    public WingetPackageInstallService(IWingetLocator locator)
    {
        _locator = locator;
    }

    public async Task<WingetInstallOutcome> InstallAsync(string packageId, string? source, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return WingetInstallOutcome.Fail("Instalacao winget disponivel apenas no Windows.");
        }

        string trimmedId = packageId.Trim();
        if (string.IsNullOrEmpty(trimmedId))
        {
            return WingetInstallOutcome.Fail("ID do pacote invalido.");
        }

        string? wingetPath = _locator.TryLocate();
        if (string.IsNullOrWhiteSpace(wingetPath))
        {
            return WingetInstallOutcome.Fail("winget.exe nao encontrado.");
        }

        string escapedId = trimmedId.Replace("\"", "\\\"", StringComparison.Ordinal);
        var args = new System.Text.StringBuilder();
        args.Append("install --id \"").Append(escapedId).Append("\" -e --accept-package-agreements --accept-source-agreements");

        string? s = source?.Trim();
        if (!string.IsNullOrEmpty(s) &&
            (s.Equals("winget", StringComparison.OrdinalIgnoreCase) || s.Equals("msstore", StringComparison.OrdinalIgnoreCase)))
        {
            args.Append(" --source ").Append(s);
        }

        ProcessRunResult run;
        try
        {
            run = await ProcessRunner.RunAsync(wingetPath, args.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return WingetInstallOutcome.Cancelled();
        }

        string combined = run.StandardOutput + run.StandardError;
        string sanitized = WingetInstallOutputSanitizer.SanitizeForLog(combined);
        if (run.ExitCode == 0)
        {
            return WingetInstallOutcome.Ok(
                string.IsNullOrWhiteSpace(sanitized)
                    ? "Instalacao concluida."
                    : sanitized);
        }

        string detail = string.IsNullOrWhiteSpace(sanitized) ? combined.Trim() : sanitized;
        if (detail.Length > 12000)
        {
            detail = detail.Substring(0, 12000) + "\n... (truncado)";
        }

        return WingetInstallOutcome.Fail($"winget install falhou (exit {run.ExitCode}).", string.IsNullOrEmpty(detail) ? null : detail);
    }
}
