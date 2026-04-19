namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

public sealed record AppInstallerStatus(
    WingetStatus Winget,
    PackageStatus AppInstaller,
    PackageStatus VcLibs,
    PackageStatus UiXaml)
{
    /// <summary>
    /// App Installer saudavel = winget localizado + os 3 pacotes Appx presentes.
    /// </summary>
    public bool Healthy =>
        Winget.Found && AppInstaller.Installed && VcLibs.Installed && UiXaml.Installed;
}

public sealed record WingetStatus(bool Found, string? Path, string? Version, string? Error);

public sealed record PackageStatus(bool Installed, string? Version, string? FullName);
