using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BananaSuisa.App.Behaviors;

/// <summary>
/// Encaminha a roda do rato para o <see cref="ScrollViewer"/> ancestral quando a grelha
/// não precisa (ou já não pode) usar o scroll interno — evita que o DataGrid "coma" a roda sobre área vazia.
/// </summary>
public static class DataGridWheelBehavior
{
    public static readonly DependencyProperty ForwardToParentScrollViewerProperty =
        DependencyProperty.RegisterAttached(
            "ForwardToParentScrollViewer",
            typeof(bool),
            typeof(DataGridWheelBehavior),
            new PropertyMetadata(false, OnForwardChanged));

    public static void SetForwardToParentScrollViewer(DependencyObject element, bool value) =>
        element.SetValue(ForwardToParentScrollViewerProperty, value);

    public static bool GetForwardToParentScrollViewer(DependencyObject element) =>
        (bool)element.GetValue(ForwardToParentScrollViewerProperty);

    private static void OnForwardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dg)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            dg.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            dg.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid dg)
        {
            return;
        }

        var innerScroll = ScrollWheelTreeHelper.FindVisualChild<ScrollViewer>(dg);
        if (innerScroll is { ScrollableHeight: > 0 })
        {
            if (e.Delta > 0 && innerScroll.VerticalOffset > 0.5)
            {
                return;
            }

            if (e.Delta < 0 && innerScroll.VerticalOffset + 0.5 < innerScroll.ScrollableHeight)
            {
                return;
            }
        }

        var outer = ScrollWheelTreeHelper.FindAncestorScrollViewer(dg);
        if (outer == null)
        {
            return;
        }

        outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }
}
