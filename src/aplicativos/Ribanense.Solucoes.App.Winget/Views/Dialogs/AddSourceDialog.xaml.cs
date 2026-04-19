using System.Windows;

namespace Ribanense.Solucoes.App.Winget.Views.Dialogs;

public partial class AddSourceDialog : Window
{
    public AddSourceDialog()
    {
        InitializeComponent();
    }

    public string SourceName => NameBox.Text ?? string.Empty;
    public string Argument => ArgBox.Text ?? string.Empty;
    public string Type => TypeBox.Text ?? string.Empty;

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(ArgBox.Text))
        {
            MessageBox.Show("Preencha nome e URL/argumento.", "Adicionar fonte", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
