using System.Windows.Input;

namespace Ribanense.Solucoes.UI.Mvvm;

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
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

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Avisa na hora. O <see cref="CommandManager"/> só reavalia no próximo
    /// input, e o primeiro clique num botão opaco era descartado.
    /// </summary>
    public void RaiseCanExecuteChanged() =>
        _canExecuteChanged?.Invoke(this, EventArgs.Empty);
}
