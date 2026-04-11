using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BananaSuisa.App.Controls;

public partial class SearchTextButtonRow : UserControl
{
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(SearchTextButtonRow),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SearchCommandProperty =
        DependencyProperty.Register(
            nameof(SearchCommand),
            typeof(ICommand),
            typeof(SearchTextButtonRow),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SearchButtonTextProperty =
        DependencyProperty.Register(
            nameof(SearchButtonText),
            typeof(string),
            typeof(SearchTextButtonRow),
            new PropertyMetadata("Pesquisar"));

    public static readonly DependencyProperty WatermarkTextProperty =
        DependencyProperty.Register(
            nameof(WatermarkText),
            typeof(string),
            typeof(SearchTextButtonRow),
            new PropertyMetadata("Buscar"));

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public string SearchButtonText
    {
        get => (string)GetValue(SearchButtonTextProperty);
        set => SetValue(SearchButtonTextProperty, value);
    }

    public string WatermarkText
    {
        get => (string)GetValue(WatermarkTextProperty);
        set => SetValue(WatermarkTextProperty, value);
    }

    public SearchTextButtonRow()
    {
        InitializeComponent();
    }
}
