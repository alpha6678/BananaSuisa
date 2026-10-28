using System;
using System.Collections.Generic;
using System.Linq;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Xunit;

namespace Ribanense.Solucoes.App.Recuperacao.Tests;

public class PowerShellProbeTests
{
    [Fact]
    public void ParseJsonList_handles_array()
    {
        const string json = """[{"Driver":"oem1.inf","ProviderName":"Realtek","Version":"6.0.1","Date":"2025-01-01"}]""";

        var rows = PowerShellProbe.ParseJsonList<DriverRow>(json);

        Assert.Single(rows);
        Assert.Equal("oem1.inf", rows[0].Driver);
    }

    [Fact]
    public void ParseJsonList_wraps_single_object()
    {
        const string json = """{"Driver":"oem2.inf","ProviderName":"ESS","Version":"1.0","Date":null}""";

        var rows = PowerShellProbe.ParseJsonList<DriverRow>(json);

        Assert.Single(rows);
        Assert.Equal("ESS", rows[0].ProviderName);
    }

    [Fact]
    public void ParseJsonList_empty_output_returns_empty_list()
    {
        Assert.Empty(PowerShellProbe.ParseJsonList<DriverRow>(""));
        Assert.Empty(PowerShellProbe.ParseJsonList<DriverRow>("   \r\n"));
    }

    [Fact]
    public void ParseJsonList_recovers_json_after_warning_lines()
    {
        const string json = """
            AVISO: algo aconteceu
            {"Driver":"oem3.inf","ProviderName":"Intel","Version":"2.0","Date":null}
            """;

        var rows = PowerShellProbe.ParseJsonList<DriverRow>(json);

        Assert.Single(rows);
        Assert.Equal("Intel", rows[0].ProviderName);
    }

    [Fact]
    public void ParseJsonObject_reads_composite_bundle()
    {
        const string json = """{"controladores":[],"media":[{"Status":"Error","Problem":28}],"endpoints":0,"pnputilProblemas":[]}""";

        var bundle = PowerShellProbe.ParseJsonObject<PnpBundleFake>(json);

        Assert.NotNull(bundle);
        Assert.Single(bundle!.Media);
        Assert.Equal(28u, bundle.Media[0].Problem);
    }

    [Fact]
    public void NormalizePsDate_converts_microsoft_epoch()
    {
        // 2025-01-01 16:00 UTC (13:00 em UTC-3) — cai no mesmo dia em qualquer fuso -3/+0.
        string? normalizada = PowerShellProbe.NormalizePsDate("/Date(1735747200000)/");

        Assert.Equal("01/01/2025", normalizada);
    }

    [Fact]
    public void NormalizePsDate_passes_through_plain_text()
    {
        Assert.Equal("2025-01-01", PowerShellProbe.NormalizePsDate("2025-01-01"));
        Assert.Null(PowerShellProbe.NormalizePsDate(null));
    }

    [Fact]
    public void BuildArguments_escapes_double_quotes()
    {
        string args = PowerShellProbe.BuildArguments("Write-Output \"oi\"");

        Assert.Contains("-ExecutionPolicy Bypass", args);
        Assert.Contains("Write-Output \\\"oi\\\"", args);
    }

    private sealed record DriverRow(string? Driver, string? ProviderName, string? Version, string? Date);

    private sealed record PnpBundleFake(List<PnpRowFake>? Media, int? Endpoints);

    private sealed record PnpRowFake(string? Status, uint? Problem);
}
