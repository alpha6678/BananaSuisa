using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BananaSuisa.App.Behaviors;

/// <summary>
/// Quando o <see cref="TextBox"/> não precisa (ou já não pode) usar o scroll interno,
/// encaminha a roda para o <see cref="ScrollViewer"/> da área principal (<c>MainContentArea</c>).
/// </summary>
public static class TextBoxWheelBehavior
{
    public const string MainContentAreaName = "MainContentArea";

    public static readonly DependencyProperty ForwardOverflowToMainContentScrollViewerProperty =
        DependencyProperty.RegisterAttached(
            "ForwardOverflowToMainContentScrollViewer",
            typeof(bool),
            typeof(TextBoxWheelBehavior),
            new PropertyMetadata(false, OnForwardChanged));

    public static void SetForwardOverflowToMainContentScrollViewer(DependencyObject element, bool value) =>
        element.SetValue(ForwardOverflowToMainContentScrollViewerProperty, value);

    public static bool GetForwardOverflowToMainContentScrollViewer(DependencyObject element) =>
        (bool)element.GetValue(ForwardOverflowToMainContentScrollViewerProperty);

    private static void OnForwardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            tb.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            tb.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not TextBox tb)
        {
            return;
        }

        var innerScroll = ScrollWheelTreeHelper.FindVisualChild<ScrollViewer>(tb);
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

        var window = Window.GetWindow(tb);
        if (window?.FindName(MainContentAreaName) is not ContentControl mainContent)
        {
            return;
        }

        var outer = ScrollWheelTreeHelper.FindScrollViewerInMainContent(mainContent);
        if (outer == null)
        {
            return;
        }

        outer.ScrollToVerticalOffset(outer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }
}
