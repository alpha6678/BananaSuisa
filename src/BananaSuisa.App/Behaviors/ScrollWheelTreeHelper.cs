using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BananaSuisa.App.Behaviors;

internal static class ScrollWheelTreeHelper
{
    internal static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    internal static ScrollViewer? FindAncestorScrollViewer(DependencyObject? start)
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current != null)
        {
            if (current is ScrollViewer sv)
            {
                return sv;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Primeiro <see cref="ScrollViewer"/> na árvore visual da view injetada em <c>MainContentArea</c>.
    /// </summary>
    internal static ScrollViewer? FindScrollViewerInMainContent(ContentControl mainContentArea)
    {
        if (mainContentArea.Content is not DependencyObject root)
        {
            return null;
        }

        return root is ScrollViewer sv ? sv : FindVisualChild<ScrollViewer>(root);
    }
}
