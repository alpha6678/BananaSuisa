namespace BananaSuisa.App.ViewModels;

/// <summary>
/// Registo de resultado (sucesso ou falha) de uma tentativa de instalação dentro de um lote.
/// </summary>
public sealed record InstallBatchResultEntry(
    string Name,
    string Id,
    string Source,
    bool Success,
    string Message,
    string? FailureDetail = null);
