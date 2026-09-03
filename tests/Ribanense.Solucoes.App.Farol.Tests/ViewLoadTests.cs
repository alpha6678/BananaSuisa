using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Farol.Views;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

/// <summary>
/// Carrega cada superfície com os recursos reais do <c>App.xaml</c>.
/// </summary>
/// <remarks>
/// O compilador valida a sintaxe do XAML, mas não que um <c>StaticResource</c>
/// exista: uma chave de estilo errada só falharia quando o usuário abre a aba.
/// Este teste transforma isso em erro de build. Roda em STA porque WPF exige.
/// </remarks>
[Collection(nameof(WpfCollection))]
public class ViewLoadTests
{
    [Fact]
    public void Mapa_carrega() => AssertLoads(() => new MapView());

    [Fact]
    public void Dossie_carrega() => AssertLoads(() => new DossierView());

    [Fact]
    public void Comparar_carrega() => AssertLoads(() => new CompareView());

    [Fact]
    public void Ajustes_carrega() => AssertLoads(() => new SettingsView());

    [Fact]
    public void Janela_principal_carrega() => AssertLoads(() => new MainWindow());

    private static void AssertLoads(Func<object> create)
    {
        Exception? failure = WpfHost.Run(() =>
        {
            object view = create();
            if (view is FrameworkElement element)
            {
                // Força a aplicação dos estilos e a resolução dos StaticResource.
                element.Measure(new Size(1280, 800));
            }
        });

        Assert.Null(failure);
    }
}

[CollectionDefinition(nameof(WpfCollection), DisableParallelization = true)]
public sealed class WpfCollection
{
}

/// <summary>
/// Thread STA compartilhada com uma instância de <see cref="Application"/> viva.
/// WPF só permite uma por processo, então ela é criada uma vez e reutilizada.
/// </summary>
internal static class WpfHost
{
    private static readonly object Gate = new();
    private static Dispatcher? _dispatcher;

    public static Exception? Run(Action action)
    {
        Dispatcher dispatcher = EnsureStarted();
        Exception? failure = null;

        dispatcher.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });

        return failure;
    }

    private static Dispatcher EnsureStarted()
    {
        lock (Gate)
        {
            if (_dispatcher is not null) return _dispatcher;

            using var ready = new ManualResetEventSlim();

            var thread = new Thread(() =>
            {
                var app = new Farol.App();
                app.InitializeComponent();

                _dispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();

                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "farol-wpf-tests",
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait(TimeSpan.FromSeconds(30));

            return _dispatcher ?? throw new InvalidOperationException("Dispatcher WPF não iniciou.");
        }
    }
}
