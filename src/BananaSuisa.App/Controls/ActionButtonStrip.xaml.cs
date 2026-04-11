using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BananaSuisa.App.Controls;

public partial class ActionButtonStrip : UserControl
{
    public static readonly DependencyProperty PrimaryButtonTextProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonText),
            typeof(string),
            typeof(ActionButtonStrip),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PrimaryButtonCommandProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonCommand),
            typeof(ICommand),
            typeof(ActionButtonStrip),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SecondaryButtonTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryButtonText),
            typeof(string),
            typeof(ActionButtonStrip),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SecondaryButtonCommandProperty =
        DependencyProperty.Register(
            nameof(SecondaryButtonCommand),
            typeof(ICommand),
            typeof(ActionButtonStrip),
            new PropertyMetadata(null));

    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    public ICommand? PrimaryButtonCommand
    {
        get => (ICommand?)GetValue(PrimaryButtonCommandProperty);
        set => SetValue(PrimaryButtonCommandProperty, value);
    }

    public string SecondaryButtonText
    {
        get => (string)GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public ICommand? SecondaryButtonCommand
    {
        get => (ICommand?)GetValue(SecondaryButtonCommandProperty);
        set => SetValue(SecondaryButtonCommandProperty, value);
    }

    public ActionButtonStrip()
    {
        InitializeComponent();
        
        // Setup visibility triggers based on whether the text is provided
        Loaded += (s, e) => UpdateButtonVisibility();
        
        // We could also watch for property changes, but for simplicity in MVVM scenarios
        // where these bindings are set once, this is usually sufficient.
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == PrimaryButtonTextProperty || e.Property == SecondaryButtonTextProperty)
        {
            UpdateButtonVisibility();
        }
    }

    private void UpdateButtonVisibility()
    {
        if (PrimaryBtn != null)
        {
            PrimaryBtn.Visibility = string.IsNullOrEmpty(PrimaryButtonText) ? Visibility.Collapsed : Visibility.Visible;
        }
        if (SecondaryBtn != null)
        {
            SecondaryBtn.Visibility = string.IsNullOrEmpty(SecondaryButtonText) ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
