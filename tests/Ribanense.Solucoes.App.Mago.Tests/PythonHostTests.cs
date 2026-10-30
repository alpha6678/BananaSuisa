using Ribanense.Solucoes.App.Mago;
using Ribanense.Solucoes.PluginSDK;
using Xunit;

namespace Ribanense.Solucoes.App.Mago.Tests;

public class PythonHostTests
{
    private static readonly AppPaths Paths = new(
        "com.ribanense.mago",
        @"C:\apps\mago",
        @"C:\dados\mago",
        @"C:\dados\mago\mago.dat");

    [Fact]
    public void FindInterpreter_para_no_primeiro_candidato_disponivel()
    {
        var sondados = new List<string>();

        PythonInterpreter? python = PythonHost.FindInterpreter((file, _) =>
        {
            sondados.Add(file);
            return file == "python";
        });

        Assert.NotNull(python);
        Assert.Equal("python", python!.FileName);
        Assert.Empty(python.PrefixArgs);
        Assert.Equal(new[] { "pythonw", "pyw", "python" }, sondados);
    }

    [Fact]
    public void FindInterpreter_devolve_null_quando_nao_ha_python()
    {
        Assert.Null(PythonHost.FindInterpreter((_, _) => false));
    }

    [Fact]
    public void BuildStartInfo_entrega_script_e_variaveis_do_sdk()
    {
        const string script = @"C:\apps\mago\py\mago_da_performance.py";
        var python = new PythonInterpreter("pyw", new[] { "-3" });

        var info = PythonHost.BuildStartInfo(python, script, Paths);

        Assert.Equal("pyw", info.FileName);
        Assert.Equal(new[] { "-3", script }, info.ArgumentList);
        Assert.Equal(Paths.AppData, info.WorkingDirectory);
        Assert.Equal(Paths.AppHome, info.Environment["RIBANENSE_APP_HOME"]);
        Assert.Equal(Paths.AppData, info.Environment["RIBANENSE_APP_DATA"]);
        Assert.False(info.UseShellExecute);
        Assert.True(info.CreateNoWindow);
    }

    [Fact]
    public void BuildStandaloneStartInfo_abre_o_exe_sem_python()
    {
        const string exe = @"C:\apps\mago\MagoDaPerformance.exe";

        var info = PythonHost.BuildStandaloneStartInfo(exe, Paths);

        Assert.Equal(exe, info.FileName);
        Assert.Empty(info.ArgumentList);
        Assert.Equal(Paths.AppData, info.WorkingDirectory);
        Assert.Equal(Paths.AppHome, info.Environment["RIBANENSE_APP_HOME"]);
        Assert.Equal(Paths.AppData, info.Environment["RIBANENSE_APP_DATA"]);
        Assert.False(info.UseShellExecute);
    }
}
