using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

public interface IElevationService
{
    bool IsAdmin { get; }

    /// <summary>
    /// Reabre o app elevado (UAC) preservando a fase a autoexecutar. Retorna false
    /// quando o usuário cancela o UAC. Em caso de sucesso a instância atual encerra.
    /// </summary>
    bool RelaunchElevated(string? autoRunPhase = null);
}

/// <summary>
/// Elevação por relançamento do próprio executável com Verb=runas, preservando
/// parâmetros — mesmo fluxo do bloco [UAC] do script de referência.
/// </summary>
public sealed class ElevationService : IElevationService
{
    public bool IsAdmin => AdminCheck.IsCurrentUserAdmin;

    public bool RelaunchElevated(string? autoRunPhase = null)
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return false;

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal,
        };
        psi.ArgumentList.Add("--elevated");
        if (!string.IsNullOrWhiteSpace(autoRunPhase))
        {
            psi.ArgumentList.Add("--run");
            psi.ArgumentList.Add(autoRunPhase);
        }

        try
        {
            Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // UAC cancelado pelo usuário.
            return false;
        }

        System.Windows.Application.Current?.Shutdown();
        return true;
    }
}
