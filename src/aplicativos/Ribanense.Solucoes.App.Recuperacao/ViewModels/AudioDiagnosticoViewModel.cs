using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Linha de exibição de uma seção do diagnóstico.</summary>
public sealed class DiagnosticoSecaoViewModel
{
    public DiagnosticoSecaoViewModel(DiagnosticoSecao secao)
    {
        Titulo = secao.Titulo;
        Itens = [.. secao.Itens];
        Erro = secao.Erro;
    }

    public string Titulo { get; }
    public ObservableCollection<CheckItem> Itens { get; }
    public string? Erro { get; }
}

/// <summary>Fase 1: diagnóstico somente leitura com relatório salvo na execução.</summary>
public sealed class AudioDiagnosticoViewModel : FaseViewModelBase
{
    private readonly IAudioDiagnosticsService _service;
    private readonly RunContext _run;
    private DiagnosticoRelatorio? _relatorio;

    public AudioDiagnosticoViewModel(BusyGate gate, Action<string> log, IAudioDiagnosticsService service, RunContext run)
        : base(gate, log)
    {
        _service = service;
        _run = run;
        ExecutarCommand = new AsyncRelayCommand(() => ExecutarFluxoAsync(), () => !gate.IsBusy);
    }

    public ICommand ExecutarCommand { get; }

    public ObservableCollection<DiagnosticoSecaoViewModel> Secoes { get; } = [];

    public string Resumo { get; private set; } = "Diagnóstico ainda não executado nesta sessão.";

    public string? CaminhoRelatorio { get; private set; }

    public DiagnosticoRelatorio? Relatorio => _relatorio;

    public void ExecutarAsyncPublic() => _ = ExecutarFluxoAsync();

    private async Task ExecutarFluxoAsync()
    {
        await ExecutarAsync("Diagnóstico", async ct =>
        {
            Secoes.Clear();
            var progress = new Progress<OperationEvent>(e =>
            {
                Log(OperationEventSink.Format(e));
                if (e.Kind == OperationEventKind.Info) Progresso = e.Message;
            });

            _relatorio = await _service.RunAsync(progress, ct).ConfigureAwait(true);
            foreach (DiagnosticoSecao secao in _relatorio.Secoes)
            {
                Secoes.Add(new DiagnosticoSecaoViewModel(secao));
            }

            Resumo = $"{_relatorio.TotalErros} erro(s), {_relatorio.TotalAvisos} aviso(s)"
                + (_relatorio.Elevado ? " — executado como administrador." : " — sem elevação (Driver Store limitado).");
            CaminhoRelatorio = SalvarRelatorio(_relatorio);
            Log($"Relatório salvo em: {CaminhoRelatorio}");
        }).ConfigureAwait(true);
    }

    private string SalvarRelatorio(DiagnosticoRelatorio relatorio)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        string jsonPath = _run.FilePath("diagnostico.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(relatorio, jsonOptions), Encoding.UTF8);

        var md = new StringBuilder();
        md.AppendLine($"# Diagnóstico de áudio — {relatorio.GeradoEm:dd/MM/yyyy HH:mm:ss}");
        md.AppendLine($"Elevado: {(relatorio.Elevado ? "sim" : "não")}");
        foreach (DiagnosticoSecao secao in relatorio.Secoes)
        {
            md.AppendLine();
            md.AppendLine($"## {secao.Titulo}");
            if (secao.Erro is not null) md.AppendLine($"(seção com erro: {secao.Erro})");
            foreach (CheckItem item in secao.Itens)
            {
                string marcador = item.Severity switch
                {
                    CheckSeverity.Ok => "✅",
                    CheckSeverity.Info => "ℹ️",
                    CheckSeverity.Warning => "⚠️",
                    _ => "❌",
                };
                md.AppendLine($"- {marcador} **{item.Title}** — {item.Detail}");
                if (item.Hint is not null) md.AppendLine($"  - Dica: {item.Hint}");
            }
        }
        string mdPath = _run.FilePath("diagnostico.md");
        File.WriteAllText(mdPath, md.ToString(), Encoding.UTF8);
        return mdPath;
    }
}
