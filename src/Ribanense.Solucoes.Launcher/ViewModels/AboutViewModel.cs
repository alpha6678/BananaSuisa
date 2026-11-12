using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.UI.Mvvm;
using Sdk = Ribanense.Solucoes.PluginSDK.SdkVersion;

namespace Ribanense.Solucoes.Launcher.ViewModels;

/// <summary>
/// Identidade do produto e desinstalação do launcher. As operações do dia a dia
/// (atualizar, tornar portátil, pastas) ficam em <see cref="SettingsViewModel"/>.
/// </summary>
public sealed class AboutViewModel : PageViewModel
{
    private readonly LauncherSelfInstaller _deployment = new();

    public AboutViewModel()
    {
        LauncherVersionText = SafeGet(() => AppVersion.ForEntry(), fallback: "0.0.0");
        SdkVersionText = SafeGet(() => Sdk.Current, fallback: "0.0.0");

        RepositoryUrl = SafeGet(() => LauncherConfig.RepositoryUrl, fallback: string.Empty);
        ReleasesUrl = SafeGet(() => LauncherConfig.ReleasesUrl, fallback: string.Empty);
        DataRoot = SafeGet(() => LauncherConfig.LauncherDataRoot, fallback: string.Empty);

        DeploymentModeLabel = LauncherConfig.Deployment.ModeLabel;
        InstallDirectory = LauncherConfig.Deployment.InstallDirectory;
        ExecutablePath = LauncherConfig.Deployment.ExecutablePath;

        OpenRepositoryCommand = new RelayCommand(_ => OpenUrl(RepositoryUrl));
        OpenReleasesCommand = new RelayCommand(_ => OpenUrl(ReleasesUrl));
        UninstallCommand = new RelayCommand(_ => Uninstall(), _ => !IsUninstalling && CanUninstall);
    }

    /// <summary>Sem instalação e sem dados marcados não há o que remover.</summary>
    public bool CanUninstall =>
        RemoveDataOnUninstall || LauncherConfig.Deployment.Mode != DeploymentMode.Loose;

    public override string Title => "Sobre";
    public override string Icon => "ℹ";

    public string ProductName => "Ribanense Soluções";
    public string LauncherVersionText { get; }
    public string SdkVersionText { get; }

    public string RepositoryUrl { get; }
    public string ReleasesUrl { get; }
    public string DataRoot { get; }

    public string DeploymentModeLabel { get; }
    public string InstallDirectory { get; }
    public string ExecutablePath { get; }

    /// <summary>Parágrafo curto sobre o modelo de distribuição, para orientar quem abre a tela.</summary>
    public string DescriptionText =>
        "Launcher de aplicativos modulares. Cada app é um executável independente, " +
        "instalado sob demanda a partir das releases do projeto, com atualização separada por app.";

    public ICommand OpenRepositoryCommand { get; }
    public ICommand OpenReleasesCommand { get; }
    public ICommand UninstallCommand { get; }

    private bool _removeDataOnUninstall;
    /// <summary>
    /// Quando marcado, a desinstalação também apaga <c>Launcher.dat</c>, os apps
    /// instalados e os dados que eles gravaram.
    /// </summary>
    public bool RemoveDataOnUninstall
    {
        get => _removeDataOnUninstall;
        set
        {
            if (!SetProperty(ref _removeDataOnUninstall, value)) return;

            OnPropertyChanged(nameof(UninstallHintText));
            OnPropertyChanged(nameof(CanUninstall));
            (UninstallCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string UninstallHintText
    {
        get
        {
            if (LauncherConfig.Deployment.Mode == DeploymentMode.Loose)
            {
                return RemoveDataOnUninstall
                    ? $"Esta cópia não está instalada nem marcada como portátil: só os dados de {DataRoot} serão apagados e o launcher fechará. Os arquivos do programa você apaga manualmente."
                    : "Esta cópia não está instalada nem marcada como portátil e não há o que remover. Para levar o launcher a outro lugar, use Configurações → Instalar nesta máquina ou Tornar portátil.";
            }

            return RemoveDataOnUninstall
                ? "Remove o launcher, seus atalhos, os apps instalados e todos os dados gravados por eles. Não dá para desfazer."
                : "Remove o launcher e seus atalhos. Os dados e os apps instalados permanecem no disco e voltam a aparecer se você reinstalar.";
        }
    }

    private bool _isUninstalling;
    public bool IsUninstalling
    {
        get => _isUninstalling;
        private set
        {
            if (SetProperty(ref _isUninstalling, value))
                (UninstallCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private string? _statusMessageText;
    public string? StatusMessageText
    {
        get => _statusMessageText;
        private set => SetProperty(ref _statusMessageText, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private void Uninstall()
    {
        DeploymentContext deployment = LauncherConfig.Deployment;
        const string title = "Desinstalar Ribanense Soluções";

        string message = deployment.Mode switch
        {
            DeploymentMode.Installed => RemoveDataOnUninstall
                ? "O launcher, seus atalhos, os apps instalados e TODOS os dados gravados serão apagados. O launcher fecha em seguida.\n\nContinuar?"
                : "O launcher e seus atalhos serão removidos. Os dados em %LOCALAPPDATA% permanecem.\n\nContinuar?",
            DeploymentMode.Portable => RemoveDataOnUninstall
                ? "A marca de cópia portátil será removida. Os arquivos do programa ficam na pasta, mas os apps instalados e os dados gravados serão apagados.\n\nContinuar?"
                : "A marca de cópia portátil será removida. Os arquivos e os dados ficam na pasta.\n\nContinuar?",
            _ => $"Esta cópia não está instalada, então apenas os dados em:\n{DataRoot}\n\n" +
                 "serão apagados e o launcher fecha. Os arquivos do programa você apaga manualmente.\n\nContinuar?"
        };

        var confirm = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsUninstalling = true;
        ErrorMessage = null;
        StatusMessageText = null;
        try
        {
            if (RemoveDataOnUninstall)
                PurgeAppsAndData(deployment);

            // Cópia avulsa não tem o que desinstalar; só os dados saíram.
            if (deployment.Mode == DeploymentMode.Loose)
            {
                if (!RemoveDataOnUninstall) return;
                Application.Current?.Shutdown();
                return;
            }

            LauncherDeploymentResult result = _deployment.Uninstall();
            if (!result.Success)
            {
                ErrorMessage = result.Error;
                return;
            }

            if (result.ShutdownRequested)
            {
                Application.Current?.Shutdown();
                return;
            }

            StatusMessageText = RemoveDataOnUninstall
                ? "Launcher e dados removidos."
                : "Launcher removido. Os dados foram mantidos.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Falha ao desinstalar: {ex.Message}";
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    /// <summary>
    /// Encerra os apps instalados, apaga atalhos e entradas de desinstalação de cada
    /// um e agenda a exclusão das pastas de apps e de dados. O agendamento é
    /// necessário porque este processo mantém o <c>Launcher.dat</c> aberto.
    /// </summary>
    private static void PurgeAppsAndData(DeploymentContext deployment)
    {
        var registry = new InstalledAppsRegistry();
        var registration = new InstallRegistrationService(new WindowsInstallRegistration());

        foreach (InstalledApp app in registry.Scan(deployment.AplicativosRoot))
        {
            if (AppProcessDetector.IsRunning(app.Id))
                AppProcessDetector.TryCloseRunning(app.Id, app.ExecutablePath, TimeSpan.FromSeconds(5));

            registration.RemoveApp(app.Id, app.DisplayName);
        }

        LauncherMaintenance.ScheduleDelete(deployment.AplicativosRoot);

        // Instalado: a pasta de dados fica separada da pasta do programa, então sai
        // inteira. Nas demais cópias ela pode abrigar o próprio executável, e aí só
        // o Launcher.dat é removido.
        if (deployment.Mode == DeploymentMode.Installed)
        {
            LauncherMaintenance.ScheduleDelete(deployment.DataRoot);
        }
        else
        {
            LauncherMaintenance.ScheduleDelete(LauncherMaintenance.AppDataRootFor(deployment.DataRoot));
            LauncherMaintenance.ScheduleDeleteFile(LauncherConfig.LauncherVaultPath);
        }
    }

    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        ErrorMessage = null;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Não foi possível abrir o link: {ex.Message}";
        }
    }

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
