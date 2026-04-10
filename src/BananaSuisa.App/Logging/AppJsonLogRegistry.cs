using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.App.Logging;

public static class AppJsonLogRegistry
{
    private static IAppJsonLog? _instance;

    public static void Initialize(IAppJsonLog log) => _instance = log;

    public static IAppJsonLog? TryGet() => _instance;

    public static IAppJsonLog Current => _instance ?? throw new InvalidOperationException("AppJsonLog nao inicializado.");
}
