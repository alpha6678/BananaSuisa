using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Sistema.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ActivationViewModel activationTab)
    {
        ActivationTab = activationTab ?? throw new ArgumentNullException(nameof(activationTab));
        ActivationTab.AttachUiLog(AppendLog);
        ClearLogCommand = new RelayCommand(() => LogLines.Clear());
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, ProductName));
    }

    public ActivationViewModel ActivationTab { get; }

    public ObservableCollection<string> LogLines { get; } = new();

    public ICommand ClearLogCommand { get; }
    public ICommand CopyLogCommand { get; }

    public string ProductName => "Gestor de Sistema";

    public void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
        {
            LogLines.Add(line);
        }
        else
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => LogLines.Add(line));
        }
    }
}
