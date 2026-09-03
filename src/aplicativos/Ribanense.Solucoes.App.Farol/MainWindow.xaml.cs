using System.ComponentModel;
using System.Windows;

namespace Ribanense.Solucoes.App.Farol;

public partial class MainWindow : Window
{
    /// <summary>
    /// Fechar a janela apenas esconde o farol: a malha precisa continuar de pé
    /// para os vizinhos enxergarem esta máquina. Sair de verdade é pela bandeja.
    /// </summary>
    public bool CloseToTray { get; set; } = true;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
