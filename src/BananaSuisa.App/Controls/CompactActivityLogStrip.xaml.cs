using System.Windows;
using System.Windows.Controls;

namespace BananaSuisa.App.Controls;

public partial class CompactActivityLogStrip : UserControl
{
    public static readonly DependencyProperty LogTextProperty =
        DependencyProperty.Register(
            nameof(LogText),
            typeof(string),
            typeof(CompactActivityLogStrip),
            new PropertyMetadata(string.Empty));

    public string LogText
    {
        get => (string)GetValue(LogTextProperty);
        set => SetValue(LogTextProperty, value);
    }

    public CompactActivityLogStrip()
    {
        InitializeComponent();
    }
}
