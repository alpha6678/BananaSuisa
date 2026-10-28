using System;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

/// <summary>Severidade de um achado de diagnóstico.</summary>
public enum CheckSeverity
{
    Ok,
    Info,
    Warning,
    Error,
}

/// <summary>Achado individual de diagnóstico (ou item de resultado de backup).</summary>
public sealed record CheckItem(string Id, string Title, CheckSeverity Severity, string Detail, string? Hint = null);

/// <summary>Seção do diagnóstico (equivale a um Write-Step do script).</summary>
public sealed record DiagnosticoSecao(string Titulo, IReadOnlyList<CheckItem> Itens, string? Erro = null);

/// <summary>Relatório completo da fase 1 (somente leitura).</summary>
public sealed record DiagnosticoRelatorio(
    DateTime GeradoEm,
    bool Elevado,
    IReadOnlyList<DiagnosticoSecao> Secoes)
{
    public int TotalErros => Secoes.Sum(s => s.Itens.Count(i => i.Severity == CheckSeverity.Error));
    public int TotalAvisos => Secoes.Sum(s => s.Itens.Count(i => i.Severity == CheckSeverity.Warning));
}

// ---------------------------------------------------------------------------
// Backup
// ---------------------------------------------------------------------------

public enum BackupItemStatus { Ok, Ausente, Falhou, Pendente }

/// <summary>Item de backup com status e detalhe.</summary>
public sealed record BackupItem(string Nome, BackupItemStatus Status, string Detalhe = "");

public enum BackupStatusGeral { NaoExecutado, Completo, Incompleto }

/// <summary>Resultado da fase 2.</summary>
public sealed record BackupResultado(
    BackupStatusGeral Status,
    IReadOnlyList<BackupItem> Itens,
    string PastaBackup,
    string? CaminhoManifesto,
    string? DriversExportDir);

// ---------------------------------------------------------------------------
// Reparo
// ---------------------------------------------------------------------------

public enum ReparoEstado { Pendente, Executando, Ok, Aviso, Falhou }

/// <summary>Estado corrente de um passo do reparo (para a UI acompanhar).</summary>
public sealed record ReparoPassoStatus(string Nome, ReparoEstado Estado, string? Detalhe = null);

public sealed record ReparoOpcoes(bool SkipSfc = false, bool SkipDism = false);

/// <summary>Resultado consolidado da fase 3.</summary>
public sealed record ReparoResultado(
    int PassosOk,
    int PassosFalhos,
    IReadOnlyList<ReparoPassoStatus> Passos,
    int EndpointsFinais,
    int DispositivosSomFinais);

// ---------------------------------------------------------------------------
// Escalada e codec
// ---------------------------------------------------------------------------

/// <summary>Driver MEDIA de terceiros listado na escalada.</summary>
public sealed record MediaDriverInfo(string OemInf, string Provider, string Version, string? Data);

/// <summary>Resultado da remoção de um driver.</summary>
public sealed record RemocaoDriverResultado(string OemInf, bool Ok, string Detalhe);

/// <summary>Arquivo .inf encontrado numa pasta de driver.</summary>
public sealed record CodecInfInfo(string Caminho, string Nome);

/// <summary>Resultado da instalação do codec.</summary>
public sealed record CodecInstallResultado(
    bool Instalou,
    string? CodecEstado,
    int EndpointsFinais,
    IReadOnlyList<string> Mensagens);
