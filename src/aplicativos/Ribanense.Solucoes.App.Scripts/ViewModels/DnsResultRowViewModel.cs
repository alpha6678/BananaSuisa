using Ribanense.Solucoes.App.Scripts.Scripts.Dns;

namespace Ribanense.Solucoes.App.Scripts.ViewModels;

public sealed class DnsResultRowViewModel
{
    public DnsResultRowViewModel(int rank, DnsServerBenchmarkResult result)
    {
        Rank = rank;
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public int Rank { get; }
    public DnsServerBenchmarkResult Result { get; }

    public string Label => Result.Server.Label;
    public string IpAddress => Result.Server.IpAddress;

    public string Origin => Result.Server.Origin switch
    {
        DnsServerOrigin.RedeAtual => "DNS atual da rede",
        DnsServerOrigin.Personalizado => "Personalizado",
        _ => "Público"
    };

    public string AverageDisplay => Result.AverageMs is double avg ? $"{avg:F0} ms" : "—";
    public string MedianDisplay => Result.MedianMs is double med ? $"{med:F0} ms" : "—";
    public string SuccessDisplay => $"{Result.SuccessRatePercent:F0}% ({Result.SuccessCount}/{Result.TotalCount})";

    public bool IsWinner => Rank == 1 && Result.SuccessCount > 0;
}
