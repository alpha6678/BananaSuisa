using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

/// <summary>Captura o dossiê local, mostra os achados e exporta o pacote.</summary>
public sealed class DossierViewModel : ObservableObject
{
    private readonly FarolStation _station;
    private readonly BundleExporter _exporter;
    private readonly IEvidenceExplainer _explainer;
    private readonly IAppJsonLog _log;
    private readonly Func<string, string?> _pickSaveFile;

    private bool _isCapturing;
    private string? _statusMessage;
    private string _summary = string.Empty;

    public DossierViewModel(
        FarolStation station,
        BundleExporter exporter,
        IEvidenceExplainer explainer,
        IAppJsonLog log,
        Func<string, string?>? pickSaveFile = null)
    {
        _station = station ?? throw new ArgumentNullException(nameof(station));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _explainer = explainer ?? throw new ArgumentNullException(nameof(explainer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _pickSaveFile = pickSaveFile ?? DefaultSaveDialog;

        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => !IsCapturing);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => _station.LatestBundle is not null);

        if (_station.LatestBundle is not null)
        {
            Present(_station.LatestBundle, _station.LatestFindings);
        }
    }

    public ObservableCollection<FindingViewModel> Findings { get; } = new();

    public AsyncRelayCommand CaptureCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (SetProperty(ref _isCapturing, value)) OnPropertyChanged(nameof(CanInteract));
        }
    }

    public bool CanInteract => !IsCapturing;

    public bool HasBundle => _station.LatestBundle is not null;

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CaptureLabel => _station.LatestBundle is { } bundle
        ? $"Última captura: {bundle.CapturedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}"
        : "Nenhuma captura ainda.";

    public string CollectorsLabel
    {
        get
        {
            if (_station.LatestBundle is not { } bundle) return string.Empty;

            int ok = bundle.Collectors.Count(c => c.Status == CollectorStatus.Ok);
            return $"{ok} de {bundle.Collectors.Count} sensores responderam.";
        }
    }

    private async Task CaptureAsync()
    {
        IsCapturing = true;
        StatusMessage = "Coletando evidências…";

        try
        {
            CaptureResult result = await _station.CaptureAsync(CancellationToken.None).ConfigureAwait(true);
            Present(result.Bundle, result.Findings);
            StatusMessage = $"Dossiê pronto com {result.Findings.Count} achado(s).";
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "capture", "Falha ao capturar dossiê.", ex);
            StatusMessage = "Não foi possível concluir a captura: " + ex.Message;
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private async Task ExportAsync()
    {
        if (_station.LatestBundle is not { } bundle) return;

        string? destination = _pickSaveFile(BundleExporter.SuggestFileName(bundle));
        if (destination is null) return;

        try
        {
            _exporter.Export(destination, bundle, _station.LatestFindings, _station.Peers.Snapshot());
            StatusMessage = $"Exportado para {Path.GetFileName(destination)}.";
            _log.Write(AppLogLevel.Information, "export", $"Pacote gerado em {destination}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write(AppLogLevel.Error, "export", "Falha ao exportar pacote.", ex);
            StatusMessage = "Não foi possível gravar o arquivo: " + ex.Message;
        }

        await Task.CompletedTask;
    }

    private void Present(EvidenceBundle bundle, IReadOnlyList<Finding> findings)
    {
        Findings.Clear();
        foreach (Finding finding in findings) Findings.Add(new FindingViewModel(finding));

        Summary = RuleBasedExplainer.Explain(bundle, findings);

        OnPropertyChanged(nameof(HasBundle));
        OnPropertyChanged(nameof(CaptureLabel));
        OnPropertyChanged(nameof(CollectorsLabel));
    }

    /// <summary>Reservado para a fase de IA: troca o texto do resumo por um explicador externo.</summary>
    public async Task RefreshSummaryAsync(CancellationToken ct)
    {
        if (_station.LatestBundle is not { } bundle) return;

        Summary = await _explainer
            .ExplainAsync(bundle, _station.LatestFindings, ct)
            .ConfigureAwait(true);
    }

    private static string? DefaultSaveDialog(string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = "Pacote do Farol (*.zip)|*.zip",
            DefaultExt = ".zip",
            AddExtension = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
