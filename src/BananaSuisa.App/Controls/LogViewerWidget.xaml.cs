using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace BananaSuisa.App.Controls;

public partial class LogViewerWidget : UserControl
{
    public static readonly DependencyProperty LogsProperty =
        DependencyProperty.Register(nameof(Logs), typeof(IEnumerable), typeof(LogViewerWidget), new PropertyMetadata(null, OnLogsChanged));

    public IEnumerable Logs
    {
        get => (IEnumerable)GetValue(LogsProperty);
        set => SetValue(LogsProperty, value);
    }

    public LogViewerWidget()
    {
        InitializeComponent();
    }

    private static void OnLogsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LogViewerWidget widget)
        {
            if (e.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= widget.OnLogCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += widget.OnLogCollectionChanged;
            }
        }
    }

    private void OnLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            LogScrollViewer.ScrollToEnd();
        });
    }
}