using Ribanense.Solucoes.App.Chocolatey.Services;

namespace Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;

public sealed class FakeChocolateyLocator : IChocolateyLocator
{
    private readonly string? _path;

    public FakeChocolateyLocator(string? path)
    {
        _path = path;
    }

    public string? TryLocate() => _path;
}
