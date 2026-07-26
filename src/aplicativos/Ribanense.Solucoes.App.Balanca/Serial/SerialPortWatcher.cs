using System.Management;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Observa chegada/remoção de dispositivos e sinaliza quando o conjunto de portas
/// seriais muda, para a UI atualizar a lista e encerrar/reconectar a sessão.
/// </summary>
/// <remarks>
/// Usa <c>Win32_DeviceChangeEvent</c> (WMI) para reagir a hot-plug sem depender de um
/// <c>HWND</c>. Uma porta COM USB pode inclusive trocar de número após reconexão; o
/// consumidor deve comparar por <see cref="SerialPortInfo.StableId"/> quando possível.
/// Fora do Windows, o watcher fica inerte.
/// </remarks>
public sealed class SerialPortWatcher : IDisposable
{
    private readonly object _sync = new();
    private ManagementEventWatcher? _watcher;
    private System.Threading.Timer? _debounce;
    private bool _disposed;

    /// <summary>Disparado (fora da thread de UI) quando as portas presentes mudam.</summary>
    public event Action<IReadOnlyList<SerialPortInfo>>? PortsChanged;

    public void Start()
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (_sync)
        {
            if (_watcher is not null || _disposed) return;
            try
            {
                var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WITHIN 2");
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += OnDeviceChange;
                _watcher.Start();
            }
            catch
            {
                // WMI de eventos indisponível: segue sem hot-plug automático.
                _watcher?.Dispose();
                _watcher = null;
            }
        }
    }

    private void OnDeviceChange(object sender, EventArrivedEventArgs e)
    {
        // Debounce: um único plug pode gerar vários eventos em rajada.
        lock (_sync)
        {
            if (_disposed) return;
            _debounce ??= new System.Threading.Timer(_ => RaiseChanged());
            _debounce.Change(500, System.Threading.Timeout.Infinite);
        }
    }

    private void RaiseChanged()
    {
        IReadOnlyList<SerialPortInfo> ports;
        try { ports = SerialPortEnumerator.Enumerate(); }
        catch { return; }
        PortsChanged?.Invoke(ports);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            try
            {
                if (_watcher is not null)
                {
                    _watcher.EventArrived -= OnDeviceChange;
                    _watcher.Stop();
                    _watcher.Dispose();
                }
            }
            catch { }
            _watcher = null;
            _debounce?.Dispose();
            _debounce = null;
        }
    }
}
