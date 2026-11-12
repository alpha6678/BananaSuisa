using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.Launcher.ViewModels;

/// <summary>
/// Configurações do próprio launcher: atualização, forma de instalação e pastas
/// de dados. As ações destrutivas (remover o launcher) ficam em Sobre.
/// </summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly ILauncherUpdateService _launcherUpdater;
    private readonly LauncherSelfInstaller _deployment = new();
    private ReleaseInfo? _latestRelease;

    public SettingsViewModel(ILauncherUpdateService launcherUpdater)
    {
        _launcherUpdater = launcherUpdater ?? throw new ArgumentNullException(nameof(launcherUpdater));

        LauncherVersionText = SafeGet(() => AppVersion.ForEntry(), fallback: "0.0.0");

        CatalogUrl = SafeGet(() => LauncherConfig.CatalogUrl, fallback: string.Empty);
        DataRoot = SafeGet(() => LauncherConfig.LauncherDataRoot, fallback: string.Empty);
        AplicativosRoot = SafeGet(() => LauncherConfig.AplicativosRoot, fallback: string.Empty);
        AppDataRoot = SafeGet(() => LauncherMaintenance.AppDataRootFor(LauncherConfig.LauncherDataRoot), fallback: string.Empty);

        InstallHereCommand = new RelayCommand(_ => InstallHere(), _ => CanInstallHere && !IsUpdating);
        MakePortableCommand = new RelayCommand(_ => MakePortable(), _ => CanMakePortable && !IsUpdating);
        OpenInstallFolderCommand = new RelayCommand(_ => OpenFolder(InstallDirectory));
        OpenDataFolderCommand = new RelayCommand(_ => OpenFolder(DataRoot));
        OpenAppsFolderCommand = new RelayCommand(_ => OpenFolder(AplicativosRoot));
        OpenAppDataFolderCommand = new RelayCommand(_ => OpenFolder(AppDataRoot));
        ClearAppDataCommand = new RelayCommand(_ => ClearAppData(), _ => CanClearAppData);

        CheckUpdateCommand = new AsyncRelayCommand(
            _ => CheckForUpdateAsync(silent: false),
            _ => !IsChecking && !IsUpdating);

        UpdateLauncherCommand = new AsyncRelayCommand(
            _ => UpdateLauncherAsync(),
            _ => UpdateAvailable && !IsUpdating && !IsChecking);
    }

    public override string Title => "Configurações";
    public override string Icon => "⚙";

    public string LauncherVersionText { get; }

    public string CatalogUrl { get; }
    public string DataRoot { get; }
    public string AplicativosRoot { get; }
    public string AppDataRoot { get; }
    public string DeploymentModeText { get; } = "Modo: " + LauncherConfig.Deployment.ModeLabel;
    public string InstallDirectory { get; } = LauncherConfig.Deployment.InstallDirectory;
    public bool CanInstallHere { get; } = LauncherConfig.Deployment.Mode != DeploymentMode.Installed;
    public bool CanMakePortable { get; } = LauncherConfig.Deployment.Mode != DeploymentMode.Portable;

    public ICommand CheckUpdateCommand { get; }
    public ICommand UpdateLauncherCommand { get; }
    public ICommand InstallHereCommand { get; }
    public ICommand MakePortableCommand { get; }
    public ICommand OpenInstallFolderCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenAppsFolderCommand { get; }
    public ICommand OpenAppDataFolderCommand { get; }
    public ICommand ClearAppDataCommand { get; }

    public override async Task OnActivatedAsync()
    {
        LauncherUsage usage = await Task.Run(
            () => LauncherMaintenance.Measure(AplicativosRoot, AppDataRoot)).ConfigureAwait(true);
        ApplyUsage(usage);
    }

    // Atualização -------------------------------------------------------

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
                OnPropertyChanged(nameof(UpdateStatusText));
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
                OnPropertyChanged(nameof(UpdateStatusText));
            }
        }
    }

    public string UpdateBannerText =>
        UpdateAvailable
            ? $"Atualização do launcher disponível: v{LauncherVersionText} → v{LatestVersionText}"
            : string.Empty;

    /// <summary>Estado da checagem, exibido no cartão de atualização.</summary>
    public string UpdateStatusText
    {
        get
        {
            if (UpdateAvailable)
                return $"Versão v{LatestVersionText} disponível (atual: v{LauncherVersionText}).";
            if (IsChecking) return "Verificando atualização…";
            return _updateChecked
                ? "O launcher está na versão mais recente."
                : "Atualização ainda não verificada.";
        }
    }

    private bool _updateChecked;

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
        OnPropertyChanged(nameof(UpdateStatusText));
        if (!silent) UpdateMessage = "Verificando atualização…";

        try
        {
            var release = await _launcherUpdater.CheckForUpdateAsync(CancellationToken.None).ConfigureAwait(true);
            _latestRelease = release;
            _updateChecked = true;

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
            OnPropertyChanged(nameof(UpdateStatusText));
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

    // Instalação --------------------------------------------------------

    private void InstallHere()
    {
        var confirm = MessageBox.Show(
            "O launcher será copiado para a pasta de programas do seu usuário, com atalho no Menu Iniciar e entrada em Aplicativos e recursos.\n\nContinuar?",
            "Instalar Ribanense Soluções",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        LauncherDeploymentResult result = _deployment.InstallHere();
        if (!result.Success)
        {
            UpdateError = result.Error;
            return;
        }

        if (result.ShutdownRequested)
            Application.Current?.Shutdown();
        else
            UpdateMessage = "Esta cópia já está instalada. O registro foi atualizado.";
    }

    private void MakePortable()
    {
        string? folder;
        try
        {
            folder = FolderPicker.Pick("Escolha a pasta da cópia portátil");
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
            return;
        }
        if (string.IsNullOrWhiteSpace(folder)) return;

        var confirm = MessageBox.Show(
            $"A cópia portátil ficará em:\n{folder}\n\nOs dados e os apps atuais são copiados se a pasta ainda estiver vazia. Continuar?",
            "Tornar portátil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        LauncherDeploymentResult result = _deployment.MakePortable(folder);
        if (!result.Success)
        {
            UpdateError = result.Error;
            return;
        }

        if (result.ShutdownRequested)
            Application.Current?.Shutdown();
    }

    // Pastas e dados ----------------------------------------------------

    private LauncherUsage _usage = new(0, 0, 0, 0);

    public string AppsUsageText =>
        _usage.InstalledApps == 0
            ? "Nenhum app instalado pelo launcher."
            : $"{_usage.InstalledApps} app(s) instalado(s) — {LauncherMaintenance.FormatSize(_usage.InstalledAppsBytes)}";

    public string AppDataUsageText =>
        _usage.AppDataFolders == 0
            ? "Nenhum dado de app gravado."
            : $"{_usage.AppDataFolders} pasta(s) de dados — {LauncherMaintenance.FormatSize(_usage.AppDataBytes)}";

    public bool CanClearAppData => _usage.AppDataFolders > 0;

    private string? _maintenanceMessage;
    public string? MaintenanceMessage
    {
        get => _maintenanceMessage;
        private set => SetProperty(ref _maintenanceMessage, value);
    }

    private string? _maintenanceError;
    public string? MaintenanceError
    {
        get => _maintenanceError;
        private set => SetProperty(ref _maintenanceError, value);
    }

    public void RefreshUsage()
    {
        ApplyUsage(LauncherMaintenance.Measure(AplicativosRoot, AppDataRoot));
    }

    private void ApplyUsage(LauncherUsage usage)
    {
        _usage = usage;
        OnPropertyChanged(nameof(AppsUsageText));
        OnPropertyChanged(nameof(AppDataUsageText));
        OnPropertyChanged(nameof(CanClearAppData));
        (ClearAppDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ClearAppData()
    {
        var confirm = MessageBox.Show(
            "Serão apagados os dados que cada app gravou (histórico, ajustes locais, configurações). " +
            "Os apps continuam instalados e voltam ao estado inicial.\n\nContinuar?",
            "Limpar dados dos apps",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        MaintenanceError = null;
        try
        {
            int removed = LauncherMaintenance.ClearAppData(AppDataRoot);
            MaintenanceMessage = removed == 0
                ? "Nenhuma pasta de dados pôde ser removida. Feche os apps abertos e tente de novo."
                : $"Dados removidos de {removed} app(s).";
            RefreshUsage();
        }
        catch (Exception ex)
        {
            MaintenanceError = $"Falha ao limpar os dados: {ex.Message}";
        }
    }

    private void OpenFolder(string path)
    {
        MaintenanceError = null;
        try
        {
            LauncherMaintenance.OpenInExplorer(path);
        }
        catch (Exception ex)
        {
            MaintenanceError = $"Não foi possível abrir a pasta: {ex.Message}";
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
