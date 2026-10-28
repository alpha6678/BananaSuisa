using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Linha selecionável de driver MEDIA na escalada.</summary>
public sealed class MediaDriverViewModel : ObservableObject
{
    public MediaDriverViewModel(Recovery.Audio.MediaDriverInfo info)
    {
        Info = info;
    }

    public Recovery.Audio.MediaDriverInfo Info { get; }

    public string Oem => Info.OemInf;
    public string Provider => Info.Provider;
    public string Version => Info.Version;
    public string Data => Info.Data ?? "—";
    public string Rotulo => $"{Info.OemInf} — {Info.Provider} v{Info.Version}" + (Info.Data is null ? "" : $" ({Info.Data})");

    private bool _selecionado;
    public bool Selecionado
    {
        get => _selecionado;
        set => SetProperty(ref _selecionado, value);
    }
}
