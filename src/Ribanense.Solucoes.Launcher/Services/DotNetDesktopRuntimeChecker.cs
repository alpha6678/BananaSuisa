using System.IO;
using System.Text.Json;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Checa, sem depender do CLI <c>dotnet</c>, se o .NET Desktop Runtime exigido por um app
/// framework-dependent esta instalado na maquina, inspecionando diretamente as pastas de
/// shared framework (<c>%ProgramFiles%\dotnet\shared\Microsoft.WindowsDesktop.App</c>).
/// Usado para evitar que o Launcher tente abrir um app e o usuario veja apenas o dialogo
/// nativo (e pouco confiavel) do apphost do .NET.
/// </summary>
public sealed class DotNetDesktopRuntimeChecker : IDotNetDesktopRuntimeChecker
{
    private const string FrameworkName = "Microsoft.WindowsDesktop.App";

    private readonly IReadOnlyList<string> _sharedFrameworkRoots;

    public DotNetDesktopRuntimeChecker() : this(DefaultRoots())
    {
    }

    internal DotNetDesktopRuntimeChecker(IReadOnlyList<string> sharedFrameworkRoots)
    {
        _sharedFrameworkRoots = sharedFrameworkRoots;
    }

    public RuntimeCheckResult Check(string appExecutablePath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(appExecutablePath);
            if (string.IsNullOrWhiteSpace(dir)) return RuntimeCheckResult.Satisfied;

            string configPath = Path.Combine(
                dir, Path.GetFileNameWithoutExtension(appExecutablePath) + ".runtimeconfig.json");
            if (!File.Exists(configPath)) return RuntimeCheckResult.Satisfied;

            string? requiredText = ReadRequiredWindowsDesktopVersion(configPath);
            if (requiredText is null || !Version.TryParse(requiredText, out var required))
                return RuntimeCheckResult.Satisfied;

            bool found = _sharedFrameworkRoots.Any(root => HasCompatibleVersion(root, required));
            return found ? RuntimeCheckResult.Satisfied : new RuntimeCheckResult(false, requiredText);
        }
        catch
        {
            // Checagem e' best-effort: qualquer falha aqui nao deve impedir o usuario de tentar abrir o app.
            return RuntimeCheckResult.Satisfied;
        }
    }

    private static string? ReadRequiredWindowsDesktopVersion(string configPath)
    {
        using var stream = File.OpenRead(configPath);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("runtimeOptions", out var runtimeOptions)) return null;
        if (!runtimeOptions.TryGetProperty("frameworks", out var frameworks) ||
            frameworks.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var framework in frameworks.EnumerateArray())
        {
            if (framework.TryGetProperty("name", out var nameEl) &&
                string.Equals(nameEl.GetString(), FrameworkName, StringComparison.Ordinal) &&
                framework.TryGetProperty("version", out var versionEl))
            {
                return versionEl.GetString();
            }
        }

        return null;
    }

    private static bool HasCompatibleVersion(string sharedFrameworkRoot, Version required)
    {
        if (!Directory.Exists(sharedFrameworkRoot)) return false;

        foreach (string dir in Directory.GetDirectories(sharedFrameworkRoot))
        {
            if (Version.TryParse(Path.GetFileName(dir), out var installed) &&
                installed.Major == required.Major &&
                installed >= required)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> DefaultRoots()
    {
        var roots = new List<string>();

        void AddRoot(Environment.SpecialFolder folder)
        {
            string basePath = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(basePath))
                roots.Add(Path.Combine(basePath, "dotnet", "shared", FrameworkName));
        }

        AddRoot(Environment.SpecialFolder.ProgramFiles);
        AddRoot(Environment.SpecialFolder.ProgramFilesX86);

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
            roots.Add(Path.Combine(dotnetRoot, "shared", FrameworkName));

        return roots;
    }
}
