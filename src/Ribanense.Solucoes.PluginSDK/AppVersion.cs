using System.Reflection;

namespace Ribanense.Solucoes.PluginSDK;

/// <summary>
/// Resolve a versão semântica de um assembly em runtime, com fallback para "0.0.0".
/// </summary>
public static class AppVersion
{
    public static string ForAssembly(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    public static string ForEntry()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        return ForAssembly(asm);
    }
}
