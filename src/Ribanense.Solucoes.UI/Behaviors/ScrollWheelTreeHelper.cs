using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ribanense.Solucoes.UI.Behaviors;

public static class ScrollWheelTreeHelper
{
    /// <summary>
    /// Encontra o <see cref="ScrollViewer"/> ancestral mais próximo na árvore visual.
    /// </summary>
    public static ScrollViewer? FindAncestorScrollViewer(DependencyObject start)
    {
        if (start is null) return null;

        DependencyObject? current = VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is ScrollViewer sv) return sv;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
