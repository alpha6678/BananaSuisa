using System.IO;

namespace Ribanense.Solucoes.PluginSDK.Payload;

/// <summary>
/// De onde sair uma cópia do payload desta versão. Num <c>.exe</c> single-file o
/// conteúdo é extraído para <see cref="AppContext.BaseDirectory"/>, então a
/// instalação não precisa baixar nada de novo.
/// </summary>
public static class PayloadSource
{
    /// <summary>
    /// Pasta que contém os arquivos de runtime desta cópia. Igual à pasta do
    /// executável numa publicação em pasta; no single-file, a pasta de extração.
    /// </summary>
    public static string ResolveDirectory(string? executablePath = null)
    {
        string? exe = executablePath ?? Environment.ProcessPath;
        string? exeDir = string.IsNullOrWhiteSpace(exe) ? null : Path.GetDirectoryName(exe);
        string baseDir = Path.GetFullPath(AppContext.BaseDirectory);

        if (exeDir is null) return baseDir;
        exeDir = Path.GetFullPath(exeDir);

        if (PathsEqual(exeDir, baseDir)) return exeDir;

        // Extração do single-file: os assemblies estão ao lado do BaseDirectory.
        return HasRuntimeFiles(baseDir) ? baseDir : exeDir;
    }

    public static bool HasRuntimeFiles(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;
        return Directory.EnumerateFiles(directory, "*.dll").Any();
    }

    /// <summary>
    /// Verdadeiro quando o executável é um single-file (o runtime não está solto na
    /// pasta dele) — nesse caso a atualização troca o arquivo inteiro.
    /// </summary>
    public static bool IsSelfExtracting(string? executablePath = null)
    {
        string? exe = executablePath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe)) return false;
        string dir = Path.GetDirectoryName(exe) ?? string.Empty;
        return !PathsEqual(ResolveDirectory(exe), dir);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
