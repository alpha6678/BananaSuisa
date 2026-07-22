namespace Ribanense.Solucoes.App.Sistema.Services;

/// <summary>
/// Metodos de ativacao expostos pelo app, mapeados para os argumentos de linha
/// de comando do MAS AIO (Microsoft Activation Scripts). Argumento vazio abre o
/// menu interativo do MAS.
/// </summary>
public sealed record MasMethod(string Id, string Display, string Description, string Arguments)
{
    public static readonly MasMethod Hwid = new(
        "hwid",
        "HWID — Ativar Windows (Permanente)",
        "Ativação digital permanente do Windows 10/11.",
        "/HWID");

    public static readonly MasMethod Ohook = new(
        "ohook",
        "Ohook — Ativar Office (Permanente)",
        "Ativação permanente do Office (C2R/Legacy).",
        "/Ohook");

    public static readonly MasMethod Tsforge = new(
        "tsforge",
        "TSforge — Windows / Office / ESU (Permanente)",
        "Ativação permanente via TSforge (substitui o antigo KMS38).",
        "/Z-WindowsESUOffice");

    public static readonly MasMethod KmsOnline = new(
        "kms_online",
        "KMS Online — 180 dias",
        "Ativação KMS temporária de Windows e Office (renova a cada 180 dias).",
        "/K-WindowsOffice");

    public static readonly MasMethod Troubleshoot = new(
        "troubleshoot",
        "Solucionar problemas de ativação",
        "Abre o menu do MAS para diagnóstico e correção de ativação.",
        "");

    public static IReadOnlyList<MasMethod> All { get; } =
        new[] { Hwid, Ohook, Tsforge, KmsOnline, Troubleshoot };
}
