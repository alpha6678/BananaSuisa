using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Services.Sources;
using Ribanense.Solucoes.App.Winget.Views.Dialogs;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Winget.ViewModels;

public sealed class SourcesViewModel : ObservableObject, ISourceRowHost
{
    private readonly IWingetSourceService _svc;
    private readonly IAppJsonLog _log;

    public SourcesViewModel(IWingetSourceService svc, IAppJsonLog log)
    {
        _svc = svc;
        _log = log;

        RefreshCommand = new AsyncRelayCommand(_ => ReloadAsync(), _ => !IsBusy);
        RefreshAllSourcesCommand = new AsyncRelayCommand(_ => UpdateAllAsync(), _ => !IsBusy);
        ResetAllCommand = new AsyncRelayCommand(_ => ResetAllAsync(), _ => !IsBusy);
        AddNewCommand = new AsyncRelayCommand(_ => AddNewAsync(), _ => !IsBusy);
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, "Gestor WinGet"));
    }

    public ObservableCollection<SourceRowViewModel> Rows { get; } = new();
    public ObservableCollection<string> LogLines { get; } = new();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand RefreshAllSourcesCommand { get; }
    public ICommand ResetAllCommand { get; }
    public ICommand AddNewCommand { get; }
    public ICommand CopyLogCommand { get; }

    public async Task ReloadAsync()
    {
        IsBusy = true;
        StatusMessage = "Listando fontes...";
        try
        {
            var list = await _svc.ListAsync(CancellationToken.None).ConfigureAwait(true);
            Rows.Clear();
            foreach (var s in list)
            {
                Rows.Add(new SourceRowViewModel(s, this));
            }
            StatusMessage = $"{Rows.Count} fonte(s) configurada(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Warning, "sources.list", "Falha ao listar fontes.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateAsync(SourceRowViewModel row)
    {
        row.IsBusy = true;
        row.Status = "Atualizando...";
        AppendLog($"== Atualizando fonte {row.Name} ==");
        try
        {
            var result = await _svc.UpdateAsync(row.Name, line => DispatcherAppend(line), CancellationToken.None).ConfigureAwait(true);
            row.Status = result.Success ? "Atualizada." : $"Falhou (exit {result.ExitCode}).";
        }
        catch (Exception ex)
        {
            row.Status = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Warning, "sources.update", $"Falha ao atualizar {row.Name}.", ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    public async Task RemoveAsync(SourceRowViewModel row)
    {
        var choice = MessageBox.Show(
            $"Remover a fonte '{row.Name}'?\nIsto afeta quais pacotes ficam visiveis na busca.",
            "Remover fonte",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (choice != MessageBoxResult.OK) return;

        row.IsBusy = true;
        row.Status = "Removendo...";
        AppendLog($"== Removendo fonte {row.Name} ==");
        try
        {
            var result = await _svc.RemoveAsync(row.Name, line => DispatcherAppend(line), CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                AppendLog($"OK - {row.Name} removida.");
                await ReloadAsync().ConfigureAwait(true);
            }
            else
            {
                row.Status = $"Falhou (exit {result.ExitCode}).";
                _log.Write(AppLogLevel.Warning, "sources.remove", $"remove falhou: {row.Name} exit={result.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            row.Status = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "sources.remove", $"Falha ao remover {row.Name}.", ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    private async Task UpdateAllAsync()
    {
        IsBusy = true;
        StatusMessage = "Atualizando todas as fontes...";
        AppendLog("== Atualizando todas as fontes ==");
        try
        {
            var result = await _svc.UpdateAsync(null, line => DispatcherAppend(line), CancellationToken.None).ConfigureAwait(true);
            StatusMessage = result.Success ? "Atualizacao concluida." : $"Falhou (exit {result.ExitCode}).";
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "sources.update_all", "Falha ao atualizar todas.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetAllAsync()
    {
        var confirm = MessageBox.Show(
            "Restaurar as fontes padrao do winget?\n\nIsto remove todas as fontes adicionadas e restaura os valores originais. Voce sera solicitado(a) a aprovar elevacao (UAC).",
            "Restaurar fontes",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        IsBusy = true;
        StatusMessage = "Aguardando UAC para restaurar...";
        AppendLog("== Reset de fontes (requer UAC) ==");

        var progress = new Progress<string>(line => DispatcherAppend(line));
        try
        {
            var result = await _svc.ResetAsync(progress, CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                StatusMessage = "Fontes restauradas.";
                await ReloadAsync().ConfigureAwait(true);
            }
            else
            {
                StatusMessage = string.IsNullOrWhiteSpace(result.Stderr) ? $"Falhou (exit {result.ExitCode})." : result.Stderr;
                _log.Write(AppLogLevel.Warning, "sources.reset", $"Reset falhou exit={result.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "sources.reset", "Reset lancou excecao.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddNewAsync()
    {
        var dlg = new AddSourceDialog();
        if (Application.Current?.MainWindow is Window owner)
        {
            dlg.Owner = owner;
        }
        if (dlg.ShowDialog() != true) return;

        string name = dlg.SourceName.Trim();
        string arg = dlg.Argument.Trim();
        string type = string.IsNullOrWhiteSpace(dlg.Type) ? "Microsoft.PreIndexed.Package" : dlg.Type.Trim();

        IsBusy = true;
        StatusMessage = $"Adicionando fonte {name}...";
        AppendLog($"== Adicionando fonte {name} ({arg}) ==");
        try
        {
            var result = await _svc.AddAsync(name, arg, type, line => DispatcherAppend(line), CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                StatusMessage = $"Fonte {name} adicionada.";
                await ReloadAsync().ConfigureAwait(true);
            }
            else
            {
                StatusMessage = $"Falhou (exit {result.ExitCode}).";
                _log.Write(AppLogLevel.Warning, "sources.add", $"add {name} falhou exit={result.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "sources.add", $"Falha ao adicionar {name}.", ex);
        }
        finally
        {
            IsBusy = false;
        }
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
        while (LogLines.Count > 200) LogLines.RemoveAt(0);
    }
}
