using System.Windows.Input;

namespace Ribanense.Solucoes.UI.Mvvm;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(
            _ => (execute ?? throw new ArgumentNullException(nameof(execute)))(),
            canExecute is null ? null : new Predicate<object?>(_ => canExecute()))
    {
    }

    private event EventHandler? _canExecuteChanged;

    public event EventHandler? CanExecuteChanged
    {
        add
        {
            _canExecuteChanged += value;
            CommandManager.RequerySuggested += value;
        }
        remove
        {
            _canExecuteChanged -= value;
            CommandManager.RequerySuggested -= value;
        }
    }

    /// <summary>
    /// Avisa na hora. O <see cref="CommandManager"/> só reavalia no próximo
    /// input, e o primeiro clique num botão opaco era descartado.
    /// </summary>
    public void RaiseCanExecuteChanged() =>
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter) =>
        !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter).ConfigureAwait(true);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }
}
