using System.Windows;
using System.Windows.Controls;

namespace BananaSuisa.App.Controls;

public partial class StatusFooter : UserControl
{
    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(StatusFooter), new PropertyMetadata("Pronto"));

    public static readonly DependencyProperty RightTextProperty =
        DependencyProperty.Register(nameof(RightText), typeof(string), typeof(StatusFooter), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ProgressValueProperty =
        DependencyProperty.Register(nameof(ProgressValue), typeof(double), typeof(StatusFooter), new PropertyMetadata(0.0));

    public static readonly DependencyProperty IsProgressVisibleProperty =
        DependencyProperty.Register(nameof(IsProgressVisible), typeof(bool), typeof(StatusFooter), new PropertyMetadata(false));

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string RightText
    {
        get => (string)GetValue(RightTextProperty);
        set => SetValue(RightTextProperty, value);
    }

    public double ProgressValue
    {
        get => (double)GetValue(ProgressValueProperty);
        set => SetValue(ProgressValueProperty, value);
    }

    public bool IsProgressVisible
    {
        get => (bool)GetValue(IsProgressVisibleProperty);
        set => SetValue(IsProgressVisibleProperty, value);
    }

    public StatusFooter()
    {
        InitializeComponent();
    }
}