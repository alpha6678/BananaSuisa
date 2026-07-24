using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.UI.Mvvm;
using Sdk = Ribanense.Solucoes.PluginSDK.SdkVersion;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class AboutViewModel : PageViewModel
{
    private readonly ILauncherUpdateService _launcherUpdater;
    private ReleaseInfo? _latestRelease;

    public AboutViewModel(ILauncherUpdateService launcherUpdater)
    {
        _launcherUpdater = launcherUpdater ?? throw new ArgumentNullException(nameof(launcherUpdater));

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

        CheckUpdateCommand = new AsyncRelayCommand(
            _ => CheckForUpdateAsync(silent: false),
            _ => !IsChecking && !IsUpdating);

        UpdateLauncherCommand = new AsyncRelayCommand(
            _ => UpdateLauncherAsync(),
            _ => UpdateAvailable && !IsUpdating && !IsChecking);
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
    public ICommand CheckUpdateCommand { get; }
    public ICommand UpdateLauncherCommand { get; }

    private bool _isChecking;
    public bool IsChecking
    {
        get => _isChecking;
        private set => SetProperty(ref _isChecking, value);
    }

    private bool _isUpdating;
    public bool IsUpdating
    {
        get => _isUpdating;
        private set => SetProperty(ref _isUpdating, value);
    }

    private bool _updateAvailable;
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set
        {
            if (SetProperty(ref _updateAvailable, value))
            {
                OnPropertyChanged(nameof(UpdateBannerText));
            }
        }
    }

    private string? _latestVersionText;
    public string? LatestVersionText
    {
        get => _latestVersionText;
        private set
        {
            if (SetProperty(ref _latestVersionText, value))
            {
                OnPropertyChanged(nameof(UpdateBannerText));
            }
        }
    }

    public string UpdateBannerText =>
        UpdateAvailable
            ? $"Atualização do launcher disponível: v{LauncherVersionText} → v{LatestVersionText}"
            : string.Empty;

    private double _updateProgress;
    public double UpdateProgress
    {
        get => _updateProgress;
        private set => SetProperty(ref _updateProgress, value);
    }

    private string? _updateMessage;
    public string? UpdateMessage
    {
        get => _updateMessage;
        private set => SetProperty(ref _updateMessage, value);
    }

    private string? _updateError;
    public string? UpdateError
    {
        get => _updateError;
        private set => SetProperty(ref _updateError, value);
    }

    /// <summary>
    /// Verifica se ha atualizacao do launcher. Quando <paramref name="silent"/> e' true,
    /// nao exibe mensagens de "nenhuma atualizacao"/erro (usado na inicializacao).
    /// </summary>
    public async Task CheckForUpdateAsync(bool silent)
    {
        if (IsChecking || IsUpdating) return;

        IsChecking = true;
        UpdateError = null;
        if (!silent) UpdateMessage = "Verificando atualização…";

        try
        {
            var release = await _launcherUpdater.CheckForUpdateAsync(CancellationToken.None).ConfigureAwait(true);
            _latestRelease = release;

            if (release is null)
            {
                UpdateAvailable = false;
                LatestVersionText = null;
                if (!silent) UpdateMessage = "O launcher já está atualizado.";
            }
            else
            {
                UpdateAvailable = true;
                LatestVersionText = release.Version;
                UpdateMessage = null;
            }
        }
        catch (Exception ex)
        {
            if (!silent) UpdateError = $"Falha ao verificar: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private async Task UpdateLauncherAsync()
    {
        if (_latestRelease is null || IsUpdating) return;

        var confirm = MessageBox.Show(
            $"O launcher será encerrado e reiniciado automaticamente na versão v{_latestRelease.Version}.\n\n" +
            "Salve o que for necessário antes de continuar. Deseja atualizar agora?",
            "Atualizar Ribanense Soluções",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsUpdating = true;
        UpdateError = null;
        UpdateMessage = "Baixando atualização…";
        UpdateProgress = 0;

        try
        {
            var progress = new Progress<double>(v => UpdateProgress = v * 100.0);
            var result = await _launcherUpdater
                .DownloadAndApplyAsync(_latestRelease, progress, CancellationToken.None)
                .ConfigureAwait(true);

            if (!result.Success)
            {
                UpdateError = result.Error;
                UpdateMessage = null;
                return;
            }

            UpdateMessage = "Reiniciando…";
            Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateError = $"Falha ao atualizar: {ex.Message}";
            UpdateMessage = null;
        }
        finally
        {
            IsUpdating = false;
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
