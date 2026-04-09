using System.Windows;
using System.Windows.Controls;

namespace BananaSuisa.App.Controls;

public partial class InfoCardWidget : UserControl
{
    public static readonly DependencyProperty CardTitleProperty =
        DependencyProperty.Register(nameof(CardTitle), typeof(string), typeof(InfoCardWidget), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CardContentProperty =
        DependencyProperty.Register(nameof(CardContent), typeof(object), typeof(InfoCardWidget), new PropertyMetadata(null));

    public string CardTitle
    {
        get => (string)GetValue(CardTitleProperty);
        set => SetValue(CardTitleProperty, value);
    }

    public object CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public InfoCardWidget()
    {
        InitializeComponent();
    }
}