using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(AudioViewModel audioTab)
    {
        AudioTab = audioTab ?? throw new ArgumentNullException(nameof(audioTab));
        Tabs = [new ModuleTabViewModel("Áudio", audioTab) { IsSelected = true }];
        foreach (ModuleTabViewModel tab in Tabs)
        {
            tab.PropertyChanged += OnTabPropertyChanged;
        }

        ClearLogCommand = new RelayCommand(() => LogLines.Clear());
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, ProductName));
    }

    public AudioViewModel AudioTab { get; }

    public ObservableCollection<ModuleTabViewModel> Tabs { get; }

    public ModuleTabViewModel? SelectedTab => Tabs.FirstOrDefault(t => t.IsSelected);

    public object? SelectedViewModel => SelectedTab?.Content;

    public ObservableCollection<string> LogLines { get; } = [];

    public ICommand ClearLogCommand { get; }

    public ICommand CopyLogCommand { get; }

    public string ProductName => "Recuperação do Windows";

    public string AdminStatus => AdminCheck.IsCurrentUserAdmin
        ? "administrador"
        : "sem elevação (leitura e diagnósticos funcionam; reparos exigem admin)";

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

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ModuleTabViewModel.IsSelected)) return;
        if (sender is not ModuleTabViewModel { IsSelected: true } selecionada) return;

        foreach (ModuleTabViewModel tab in Tabs.Where(t => !ReferenceEquals(t, selecionada)))
        {
            tab.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectedViewModel));
    }
}
