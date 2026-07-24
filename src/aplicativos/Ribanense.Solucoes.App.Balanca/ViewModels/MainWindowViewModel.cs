using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Balanca.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(BalancaViewModel balanca)
    {
        Balanca = balanca ?? throw new ArgumentNullException(nameof(balanca));
        Balanca.AttachUiLog(AppendLog);
        ClearLogCommand = new RelayCommand(() => LogLines.Clear());
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, ProductName));
    }

    public BalancaViewModel Balanca { get; }

    public ObservableCollection<string> LogLines { get; } = new();

    public ICommand ClearLogCommand { get; }
    public ICommand CopyLogCommand { get; }

    public string ProductName => "Testador de Balanças";

    public void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        string stamped = $"{DateTime.Now:HH:mm:ss}  {line}";
        if (System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
        {
            LogLines.Add(stamped);
        }
        else
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => LogLines.Add(stamped));
        }
    }
}
