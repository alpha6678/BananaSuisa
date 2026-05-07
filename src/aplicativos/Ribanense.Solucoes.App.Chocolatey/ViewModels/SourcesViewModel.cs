using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Chocolatey.Services.Sources;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public sealed class SourcesViewModel : ObservableObject, ISourceRowHost
{
    private readonly IChocolateySourceService _svc;
    private readonly IAppJsonLog _log;
    private readonly Action<string> _appendLog;

    public SourcesViewModel(IChocolateySourceService svc, IAppJsonLog log, Action<string> appendLog)
    {
        _svc = svc;
        _log = log;
        _appendLog = appendLog;

        RefreshCommand = new AsyncRelayCommand(_ => ReloadAsync(), _ => !IsBusy);
    }

    public ObservableCollection<SourceRowViewModel> Rows { get; } = new();

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
            var result = await _svc.RemoveAsync(row.Name, line => AppendLog(line), CancellationToken.None).ConfigureAwait(true);
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

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _appendLog(line);
    }
}
