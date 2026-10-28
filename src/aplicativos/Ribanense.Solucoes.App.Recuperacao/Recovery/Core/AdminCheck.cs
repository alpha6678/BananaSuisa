using System;
using System.Security.Principal;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

/// <summary>Verifica se o processo atual roda com privilégios de administrador.</summary>
public static class AdminCheck
{
    public static bool IsCurrentUserAdmin
    {
        get
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
