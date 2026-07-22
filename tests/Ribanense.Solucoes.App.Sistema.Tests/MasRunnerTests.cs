using System.Net;
using System.Net.Http;
using Ribanense.Solucoes.App.Sistema.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Sistema.Tests;

public class MasRunnerTests
{
    [Fact]
    public async Task RunAsync_returns_failure_when_download_throws()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "ribanense-sistema-test-" + Guid.NewGuid().ToString("N"));
        var runner = new MasRunner(cacheDir, new FakeElevated(), () => new HttpClient(new ThrowingHandler()));

        var result = await runner.RunAsync(MasMethod.Hwid, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Cancelled);
        Assert.NotNull(result.Error);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("simulado");
    }

    private sealed class FakeElevated : IElevatedCommandRunner
    {
        public Task<ElevatedResult> RunScriptAsync(string powerShellScript, IProgress<string>? onLine, CancellationToken ct)
            => throw new InvalidOperationException("nao deveria chegar aqui");
    }
}
