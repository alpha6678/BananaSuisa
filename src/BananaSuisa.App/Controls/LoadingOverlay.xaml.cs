using System.Windows;
using System.Windows.Controls;

namespace BananaSuisa.App.Controls;

public partial class LoadingOverlay : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(LoadingOverlay), new PropertyMetadata("Carregando..."));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingOverlay()
    {
        InitializeComponent();
    }
}