using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Winget.ViewModels;

public class ModuleViewModel : ObservableObject
{
    private readonly IAppInstallerDiagnostics _diag;
    private readonly IAppInstallerRepair _repair;
    private readonly IAppJsonLog _log;

    public ModuleViewModel(IAppInstallerDiagnostics diag, IAppInstallerRepair repair, IAppJsonLog log)
    {
        _diag = diag;
        _repair = repair;
        _log = log;

        _inspectCommand = new AsyncRelayCommand(_ => InspectAsync(), _ => !IsBusy);
        _reregisterCommand = new AsyncRelayCommand(_ => ReregisterAsync(), _ => !IsBusy);
        _installLatestCommand = new AsyncRelayCommand(_ => InstallLatestAsync(), _ => !IsBusy);
        _copyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, "Gestor WinGet"));
    }

    public ObservableCollection<string> LogLines { get; } = new();

    private AppInstallerStatus? _status;
    public virtual object? Status
    {
        get => _status;
        set
        {
            if (value is AppInstallerStatus s && !Equals(_status, s))
            {
                _status = s;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(WingetLine));
                OnPropertyChanged(nameof(AppInstallerLine));
                OnPropertyChanged(nameof(VcLibsLine));
                OnPropertyChanged(nameof(UiXamlLine));
                OnPropertyChanged(nameof(HealthyLabel));
                OnPropertyChanged(nameof(WingetState));
                OnPropertyChanged(nameof(AppInstallerState));
                OnPropertyChanged(nameof(VcLibsState));
                OnPropertyChanged(nameof(UiXamlState));
            }
        }
    }

    public bool HasStatus => _status is not null;

    public string WingetLine =>
        _status?.Winget is { Found: true } w
            ? $"winget {w.Version ?? "(versao desconhecida)"} — {w.Path}"
            : _status?.Winget.Error ?? "winget.exe nao encontrado.";

    public string AppInstallerLine => FormatPkg(_status?.AppInstaller, AppInstallerDiagnostics.AppInstallerName);
    public string VcLibsLine => FormatPkg(_status?.VcLibs, AppInstallerDiagnostics.VcLibsName);
    public string UiXamlLine => FormatPkg(_status?.UiXaml, AppInstallerDiagnostics.UiXamlName);

    public string WingetState => _status?.Winget.Found == true ? "Success" : "Danger";
    public string AppInstallerState => _status?.AppInstaller.Installed == true ? "Success" : "Danger";
    public string VcLibsState => _status?.VcLibs.Installed == true ? "Success" : "Warning";
    public string UiXamlState => _status?.UiXaml.Installed == true ? "Success" : "Warning";

    public string HealthyLabel => _status switch
    {
        null => "",
        { Healthy: true } => "Modulo saudavel.",
        _ => "Modulo com problema. Use um dos botoes abaixo."
    };

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private readonly ICommand _inspectCommand;
    private readonly ICommand _reregisterCommand;
    private readonly ICommand _installLatestCommand;
    private readonly ICommand _copyLogCommand;

    public virtual ICommand? InspectCommand => _inspectCommand;
    public virtual ICommand? ReregisterCommand => _reregisterCommand;
    public virtual ICommand? InstallLatestCommand => _installLatestCommand;
    public virtual ICommand? CopyLogCommand => _copyLogCommand;

    public virtual async Task InspectAsync()
    {
        IsBusy = true;
        StatusMessage = "Inspecionando modulo...";
        AppendLog("== Verificacao do modulo ==");
        try
        {
            var s = await _diag.InspectAsync(CancellationToken.None).ConfigureAwait(true);
            Status = s;
            StatusMessage = s.Healthy
                ? "Modulo saudavel."
                : "Modulo com problemas detectados.";
            AppendLog(s.Healthy ? "[OK] Tudo instalado." : "[!!] Existem pendencias.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "module.inspect", "Falha ao inspecionar modulo.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReregisterAsync()
    {
        var confirm = MessageBox.Show(
            "Re-registrar os pacotes AppX do App Installer / VCLibs / UI.Xaml?\n\n" +
            "Operacao sem rede, util quando o registro esta corrompido. Voce sera solicitado(a) a aprovar UAC.",
            "Reparar modulo",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        await RunRepairAsync(
            "Re-registrando pacotes AppX (UAC)...",
            progress => _repair.ReregisterAsync(progress, CancellationToken.None)).ConfigureAwait(true);
    }

    private async Task InstallLatestAsync()
    {
        var confirm = MessageBox.Show(
            "Baixar e instalar o App Installer mais recente da Microsoft + dependencias?\n\n" +
            "Requer rede e UAC. Pode levar alguns minutos.",
            "Instalar App Installer",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        await RunRepairAsync(
            "Baixando e instalando (UAC)...",
            progress => _repair.DownloadAndInstallLatestAsync(progress, CancellationToken.None)).ConfigureAwait(true);
    }

    private async Task RunRepairAsync(string startMessage, Func<IProgress<string>, Task<RepairResult>> op)
    {
        IsBusy = true;
        StatusMessage = startMessage;
        AppendLog($"== {startMessage} ==");

        var progress = new Progress<string>(DispatcherAppend);
        try
        {
            var result = await op(progress).ConfigureAwait(true);
            if (result.Cancelled)
            {
                StatusMessage = "Operacao cancelada pelo usuario.";
                _log.Write(AppLogLevel.Information, "module.repair", "Usuario cancelou UAC.");
            }
            else if (result.Success)
            {
                StatusMessage = "Reparo concluido. Recarregando status...";
                await InspectAsync().ConfigureAwait(true);
            }
            else
            {
                StatusMessage = $"Falhou (exit {result.ExitCode}).";
                _log.Write(AppLogLevel.Warning, "module.repair", $"Repair exit={result.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "module.repair.exception", "Repair lancou excecao.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatPkg(PackageStatus? pkg, string name)
    {
        if (pkg is null) return name + ": (desconhecido)";
        if (!pkg.Installed) return name + ": NAO instalado";
        return name + " " + (pkg.Version ?? "(sem versao)");
    }

    private void DispatcherAppend(string line)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AppendLog(line);
            return;
        }
        dispatcher.Invoke(() => AppendLog(line));
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        LogLines.Add(line);
        while (LogLines.Count > 400) LogLines.RemoveAt(0);
    }
}
