using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

/// <summary>
/// Leitura, avaliação e correção da ACL da chave MMDevices\Audio.
/// Fluxo em 3 passos (privilégio → dono → DACL) igual ao script de referência:
/// o cmdlet Set-Acl reabre a chave pelo provider SEM pedir o direito específico e
/// falha quando a DACL não concede WRITE_DAC a ninguém; como NOVO dono, a reabertura
/// com ChangePermissions é permitida e o SetAccessControl no handle funciona.
/// </summary>
public static class MmDevicesAcl
{
    private static readonly SecurityIdentifier SidSystem = new(AudioConstants.SidSystem);
    private static readonly SecurityIdentifier SidAdmins = new(AudioConstants.SidAdministrators);
    private static readonly SecurityIdentifier SidUsers = new(AudioConstants.SidUsers);
    private static readonly SecurityIdentifier SidOwnerRights = new(AudioConstants.SidOwnerRights);

    /// <summary>Avalia a ACL de uma chave aberta e devolve severidade + texto.</summary>
    public static (CheckSeverity Severidade, string Texto) Evaluate(RegistryKey baseKey)
    {
        try
        {
            RegistrySecurity acl = baseKey.GetAccessControl(AccessControlSections.Access);

            bool hasSystem = false;
            bool hasAdmin = false;
            bool ownerRights = false;
            foreach (RegistryAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType != AccessControlType.Allow) continue;
                if (!rule.RegistryRights.HasFlag(RegistryRights.FullControl)) continue;
                if (Equals(rule.IdentityReference, SidSystem)) hasSystem = true;
                if (Equals(rule.IdentityReference, SidAdmins)) hasAdmin = true;
                if (Equals(rule.IdentityReference, SidOwnerRights)) ownerRights = true;
            }

            string protecao = acl.AreAccessRulesProtected
                ? "DACL protegida (herança bloqueada)"
                : "DACL com herança";

            if (ownerRights)
                return (CheckSeverity.Error,
                    $"ACE OWNER RIGHTS (S-1-3-0) detectada: o dono só tem os direitos dessa ACE (cadeado contra o próprio dono). {protecao}.");
            if (!hasSystem)
                return (CheckSeverity.Error,
                    $"SYSTEM ({AudioConstants.SidSystem}) sem FullControl (endpoints morrem na criação). {protecao}.");
            if (!hasAdmin)
                return (CheckSeverity.Warning,
                    $"Administrators ({AudioConstants.SidAdministrators}) sem FullControl. {protecao}.");

            return (CheckSeverity.Ok, $"ACL aparentemente correta (SYSTEM e Administrators com FullControl). {protecao}.");
        }
        catch (Exception ex)
        {
            return (CheckSeverity.Warning, $"Não foi possível ler a ACL: {ex.Message}");
        }
    }

    /// <summary>
    /// Despeja o security descriptor completo (dono, herança e ACEs com SID) no
    /// arquivo indicado. Detecta ACE de OWNER RIGHTS e sinaliza no retorno.
    /// </summary>
    public static string DumpSecurityDescriptor(string registryPath, string arquivoDestino)
    {
        var linhas = new List<string>();
        using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(registryPath)
                   ?? throw new InvalidOperationException($"Chave inexistente: {registryPath}"))
        {
            RegistrySecurity acl = baseKey.GetAccessControl(
                AccessControlSections.Owner | AccessControlSections.Access);

            string ownerSid;
            try { ownerSid = acl.GetOwner(typeof(SecurityIdentifier))?.ToString() ?? "?"; }
            catch { ownerSid = acl.GetOwner(typeof(NTAccount))?.ToString() ?? "?"; }
            string groupSid;
            try { groupSid = acl.GetGroup(typeof(SecurityIdentifier))?.ToString() ?? "?"; }
            catch { groupSid = acl.GetGroup(typeof(NTAccount))?.ToString() ?? "?"; }

            linhas.Add($"Owner (SID): {ownerSid}");
            linhas.Add($"Group (SID): {groupSid}");
            linhas.Add($"DACL protegida (herança bloqueada): {acl.AreAccessRulesProtected}");
            linhas.Add($"SACL protegida: {acl.AreAuditRulesProtected}");
            linhas.Add("--- ACEs da DACL ---");
            foreach (RegistryAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                linhas.Add($"{rule.IdentityReference} | {rule.AccessControlType} | {rule.RegistryRights} | herdada={rule.IsInherited}");
                if (Equals(rule.IdentityReference, SidOwnerRights))
                {
                    linhas.Add(">>> ACE OWNER RIGHTS (S-1-3-0) DETECTADA: o dono só tem os direitos dessa ACE (cadeado contra o próprio dono).");
                }
            }
        }

        string? diretorio = Path.GetDirectoryName(arquivoDestino);
        if (!string.IsNullOrEmpty(diretorio)) Directory.CreateDirectory(diretorio);
        File.WriteAllLines(arquivoDestino, linhas);
        return string.Join(Environment.NewLine, linhas);
    }

    /// <summary>
    /// Reparo da ACL: assume a propriedade (quando necessário) e concede as ACEs
    /// padrão por SID. Retorna a lista de mensagens do que fez.
    /// </summary>
    public static List<string> Repair(IProgress<OperationEvent> events)
    {
        var mensagens = new List<string>();

        using (RegistryKey? existente = Registry.LocalMachine.OpenSubKey(AudioConstants.MmDevicesAudioPath, writable: false))
        {
            if (existente is null)
            {
                events.Report(new OperationEvent(OperationEventKind.Warning,
                    "Chave MMDevices\\Audio inexistente. O serviço de audio deve recriá-la no arranque (verifique o serviço primeiro)."));
                mensagens.Add("Chave MMDevices\\Audio inexistente — nada a corrigir agora.");
                return mensagens;
            }
        }

        // 1) Assumir a PROPRIEDADE da chave: DACL esvaziada nega WRITE_DAC mesmo a admin elevado.
        WindowsPrivilege.Enable("SeTakeOwnershipPrivilege");
        try
        {
            using RegistryKey? rk = Registry.LocalMachine.OpenSubKey(
                AudioConstants.MmDevicesAudioPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.TakeOwnership);
            if (rk is null)
            {
                events.Report(new OperationEvent(OperationEventKind.Error,
                    "Não foi possível abrir a chave para TakeOwnership."));
                mensagens.Add("Falha ao abrir a chave para TakeOwnership.");
            }
            else
            {
                RegistrySecurity sec = rk.GetAccessControl(AccessControlSections.Owner);
                IdentityReference? owner = sec.GetOwner(typeof(SecurityIdentifier));
                if (owner is not null && !Equals(owner, SidAdmins) && !Equals(owner, SidSystem))
                {
                    sec.SetOwner(SidAdmins);
                    rk.SetAccessControl(sec);
                    string msg = $"Propriedade da chave assumida para Administrators (antes: {owner}).";
                    events.Report(new OperationEvent(OperationEventKind.Ok, msg));
                    mensagens.Add(msg);
                }
                else
                {
                    string msg = $"Proprietário da chave já adequado ({owner}).";
                    events.Report(new OperationEvent(OperationEventKind.Ok, msg));
                    mensagens.Add(msg);
                }
            }
        }
        catch (Exception ex)
        {
            string msg = $"Falha ao assumir a propriedade da chave: {ex.Message}";
            events.Report(new OperationEvent(OperationEventKind.Error, msg));
            mensagens.Add(msg);
        }

        // 2) Conceder as ACEs padrão por SID ATRAVÉS DO HANDLE .NET reaberto com
        //    ChangePermissions (o provider do Set-Acl falha sem WRITE_DAC na DACL).
        var grants = new (string Nome, SecurityIdentifier Sid, RegistryRights Rights)[]
        {
            ("SYSTEM", SidSystem, RegistryRights.FullControl),
            ("Administrators", SidAdmins, RegistryRights.FullControl),
            ("Users", SidUsers, RegistryRights.ReadKey),
        };

        int falhas = 0;
        try
        {
            using RegistryKey? rk2 = Registry.LocalMachine.OpenSubKey(
                AudioConstants.MmDevicesAudioPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions);
            if (rk2 is null)
            {
                events.Report(new OperationEvent(OperationEventKind.Error,
                    "Reabertura com ChangePermissions retornou nula mesmo sendo dono (suspeita: ACE OWNER RIGHTS). Despejando o security descriptor..."));
                falhas++;
            }
            else
            {
                foreach (var grant in grants)
                {
                    try
                    {
                        RegistrySecurity acl = rk2.GetAccessControl();
                        bool jaTem = acl.GetAccessRules(true, true, typeof(SecurityIdentifier))
                            .Cast<RegistryAccessRule>()
                            .Any(r => r.AccessControlType == AccessControlType.Allow
                                      && r.RegistryRights.HasFlag(grant.Rights)
                                      && Equals(r.IdentityReference, grant.Sid));
                        if (!jaTem)
                        {
                            var nova = new RegistryAccessRule(
                                grant.Sid, grant.Rights, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow);
                            acl.AddAccessRule(nova);
                            rk2.SetAccessControl(acl);
                            string msg = $"Concedido {grant.Rights} para {grant.Nome} (por SID, via handle).";
                            events.Report(new OperationEvent(OperationEventKind.Ok, msg));
                            mensagens.Add(msg);
                        }
                        else
                        {
                            string msg = $"{grant.Nome} já possui {grant.Rights}.";
                            events.Report(new OperationEvent(OperationEventKind.Ok, msg));
                            mensagens.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        falhas++;
                        string msg = $"Falha ao conceder {grant.Rights} a {grant.Nome}: {ex.Message}";
                        events.Report(new OperationEvent(OperationEventKind.Error, msg));
                        mensagens.Add(msg);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            falhas++;
            string msg = $"Falha ao reabrir a chave com ChangePermissions: {ex.Message}";
            events.Report(new OperationEvent(OperationEventKind.Error, msg));
            mensagens.Add(msg);
        }

        if (falhas > 0)
        {
            events.Report(new OperationEvent(OperationEventKind.Warning,
                $"{falhas} concessão(ões) falharam — use o dump do security descriptor para análise."));
        }

        return mensagens;
    }
}
