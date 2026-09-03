using Microsoft.Win32;

namespace Ribanense.Solucoes.App.Farol.Services;

/// <summary>
/// Liga e desliga a inicialização automática do Farol.
/// </summary>
/// <remarks>
/// Usa <c>HKCU\...\Run</c> em vez de serviço do Windows ou tarefa agendada: não
/// precisa de administrador, some junto com o perfil do usuário e é visível no
/// Gerenciador de Tarefas, onde o usuário pode desligar sem depender do app.
/// </remarks>
public sealed class AutostartRegistrar
{
    public const string ValueName = "Ribanense Farol";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _executablePath;

    public AutostartRegistrar(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("Caminho do executável obrigatório.", nameof(executablePath));

        _executablePath = executablePath;
    }

    public string CommandLine => $"\"{_executablePath}\" --tray";

    public bool IsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string existing
                    && existing.Contains(_executablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    /// <summary>Aplica o estado desejado. Devolve <c>false</c> se o registro recusar a escrita.</summary>
    public bool Set(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

            if (enabled) key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
