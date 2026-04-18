using System.Diagnostics;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class AboutViewModel : PageViewModel
{
    public AboutViewModel()
    {
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
    public override string Icon => "ℹ";

    public string LauncherVersion => AppVersion.ForEntry();
    public string SdkVersion => PluginSDK.SdkVersion.Current;
    public string CatalogUrl => LauncherConfig.CatalogUrl;
    public string DataRoot => LauncherConfig.LauncherDataRoot;
    public string AplicativosRoot => LauncherConfig.AplicativosRoot;

    public ICommand OpenGitHubCommand { get; }
}
