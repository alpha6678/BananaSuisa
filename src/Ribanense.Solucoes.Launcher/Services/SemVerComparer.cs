using Ribanense.Solucoes.PluginSDK.Manifest;

namespace Ribanense.Solucoes.Launcher.Services;

public sealed class SemVerComparer : IComparer<string>
{
    public static readonly SemVerComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        bool xOk = SemVerLoose.IsValid(x);
        bool yOk = SemVerLoose.IsValid(y);
        if (!xOk && !yOk) return string.Compare(x, y, StringComparison.Ordinal);
        if (!xOk) return -1;
        if (!yOk) return 1;

        return SemVerLoose.Compare(x, y);
    }
}
