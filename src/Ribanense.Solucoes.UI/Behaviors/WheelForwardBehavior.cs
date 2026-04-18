using System.Windows;
using System.Windows.Input;

namespace Ribanense.Solucoes.UI.Behaviors;

/// <summary>
/// Propaga eventos da roda do mouse de um elemento (ex.: DataGrid, TextBox de log
/// multi-linha) para o <see cref="System.Windows.Controls.ScrollViewer"/> ancestral,
/// evitando que o elemento filho "engula" o scroll da página quando o conteúdo
/// interno não precisa rolar.
/// </summary>
/// <example>
/// <code>
/// xmlns:b="clr-namespace:Ribanense.Solucoes.UI.Behaviors;assembly=Ribanense.Solucoes.UI"
/// &lt;DataGrid b:WheelForwardBehavior.ForwardToParent="True" ... /&gt;
/// </code>
/// </example>
public static class WheelForwardBehavior
{
    public static readonly DependencyProperty ForwardToParentProperty =
        DependencyProperty.RegisterAttached(
            "ForwardToParent",
            typeof(bool),
            typeof(WheelForwardBehavior),
            new PropertyMetadata(false, OnForwardToParentChanged));

    public static bool GetForwardToParent(DependencyObject obj)
        => (bool)obj.GetValue(ForwardToParentProperty);

    public static void SetForwardToParent(DependencyObject obj, bool value)
        => obj.SetValue(ForwardToParentProperty, value);

    private static void OnForwardToParentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        element.PreviewMouseWheel -= OnPreviewMouseWheel;
        if (e.NewValue is true)
        {
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not UIElement element) return;

        var scrollViewer = ScrollWheelTreeHelper.FindAncestorScrollViewer(element);
        if (scrollViewer is null) return;

        e.Handled = true;
        var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };
        scrollViewer.RaiseEvent(forwarded);
    }
}
