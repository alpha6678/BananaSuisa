using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Chocolatey.Services.Sources;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public sealed class SourcesViewModel : ObservableObject, ISourceRowHost
{
    private readonly IChocolateySourceService _svc;
    private readonly IAppJsonLog _log;

    public SourcesViewModel(IChocolateySourceService svc, IAppJsonLog log)
    {
        _svc = svc;
        _log = log;

        RefreshCommand = new AsyncRelayCommand(_ => ReloadAsync(), _ => !IsBusy);
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, "Gestor Chocolatey"));
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
