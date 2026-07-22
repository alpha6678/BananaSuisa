namespace Ribanense.Solucoes.App.Sistema.Services;

/// <summary>
/// Metodos de ativacao suportados pelo MAS (Microsoft Activation Scripts).
/// Os codigos numericos seguem o menu do MAS_AIO.cmd.
/// </summary>
public sealed record MasMethod(string Id, string Display, string Description, int MenuCode)
{
    public static readonly MasMethod Hwid = new("hwid", "HWID — Ativar Windows (Permanente)", "Ativação digital permanente do Windows 10/11.", 1);
    public static readonly MasMethod Ohook = new("ohook", "Ohook — Ativar Office (Permanente)", "Ativação permanente do Office (C2R/Legacy).", 2);
    public static readonly MasMethod Kms38 = new("kms38", "KMS38 — Até 2038", "Ativação KMS válida até 2038 (sem renovação).", 3);
    public static readonly MasMethod KmsOnline = new("kms_online", "KMS Online — 180 dias", "Ativação KMS temporária (renova a cada 180 dias).", 4);
    public static readonly MasMethod Troubleshoot = new("troubleshoot", "Solucionar problemas de ativação", "Diagnóstico e correção de ativação.", 5);

    public static IReadOnlyList<MasMethod> All { get; } =
        new[] { Hwid, Ohook, Kms38, KmsOnline, Troubleshoot };
}
