using System.Diagnostics;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.UI.Mvvm;
using Sdk = Ribanense.Solucoes.PluginSDK.SdkVersion;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class AboutViewModel : PageViewModel
{
    public AboutViewModel()
    {
        LauncherVersionText = SafeGet(() => AppVersion.ForEntry(), fallback: "0.0.0");
        SdkVersionText = SafeGet(() => Sdk.Current, fallback: "0.0.0");
        VersionsLine = $"Launcher {LauncherVersionText} — SDK {SdkVersionText}";

        CatalogUrl = SafeGet(() => LauncherConfig.CatalogUrl, fallback: string.Empty);
        DataRoot = SafeGet(() => LauncherConfig.LauncherDataRoot, fallback: string.Empty);
        AplicativosRoot = SafeGet(() => LauncherConfig.AplicativosRoot, fallback: string.Empty);

        OpenGitHubCommand = new RelayCommand(_ =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/") { UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
        });
    }

    public override string Title => "Sobre";
    public override string Icon => "i";

    public string LauncherVersionText { get; }
    public string SdkVersionText { get; }
    public string VersionsLine { get; }

    public string CatalogUrl { get; }
    public string DataRoot { get; }
    public string AplicativosRoot { get; }

    public ICommand OpenGitHubCommand { get; }

    private static string SafeGet(Func<string> read, string fallback)
    {
        try
        {
            return read() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
