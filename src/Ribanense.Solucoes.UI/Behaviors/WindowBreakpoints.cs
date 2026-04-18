using System.Windows;

namespace Ribanense.Solucoes.UI.Behaviors;

/// <summary>
/// Helper que emite estados visuais responsivos (Compact / Medium / Wide)
/// conforme a largura do <see cref="FrameworkElement"/>. Use em conjunto com
/// <c>VisualStateManager.VisualStateGroups</c> definidos no XAML.
/// </summary>
/// <example>
/// <code>
/// &lt;Window xmlns:b="clr-namespace:Ribanense.Solucoes.UI.Behaviors;assembly=Ribanense.Solucoes.UI"
///         b:WindowBreakpoints.Enabled="True"&gt;
///   &lt;VisualStateManager.VisualStateGroups&gt;
///     &lt;VisualStateGroup x:Name="Layout"&gt;
///       &lt;VisualState x:Name="Compact"&gt; ... &lt;/VisualState&gt;
///       &lt;VisualState x:Name="Medium"&gt;  ... &lt;/VisualState&gt;
///       &lt;VisualState x:Name="Wide"&gt;    ... &lt;/VisualState&gt;
///     &lt;/VisualStateGroup&gt;
///   &lt;/VisualStateManager.VisualStateGroups&gt;
/// &lt;/Window&gt;
/// </code>
/// </example>
public static class WindowBreakpoints
{
    /// <summary>Largura &lt;= este valor → estado "Compact".</summary>
    public static double CompactMax { get; set; } = 768;

    /// <summary>Largura &lt;= este valor (e &gt; CompactMax) → estado "Medium".</summary>
    public static double MediumMax { get; set; } = 1200;

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(WindowBreakpoints),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;

        fe.SizeChanged -= OnSizeChanged;
        fe.Loaded -= OnLoaded;

        if (e.NewValue is true)
        {
            fe.SizeChanged += OnSizeChanged;
            if (fe.IsLoaded)
            {
                UpdateState(fe, fe.ActualWidth);
            }
            else
            {
                fe.Loaded += OnLoaded;
            }
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.Loaded -= OnLoaded;
            UpdateState(fe, fe.ActualWidth);
        }
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            UpdateState(fe, e.NewSize.Width);
        }
    }

    private static void UpdateState(FrameworkElement fe, double width)
    {
        string stateName = width <= CompactMax
            ? "Compact"
            : width <= MediumMax ? "Medium" : "Wide";

        VisualStateManager.GoToElementState(fe, stateName, true);
    }
}
