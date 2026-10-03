using System.Windows.Input;

namespace NezamMonitor.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter) { try { await _execute(); } catch { } }
    public async Task ExecuteAsync() { try { await _execute(); } catch { } }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;
    public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;
    public RelayCommand(Action<T?> execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        if (parameter is T t) _execute(t);
        else if (parameter == null) _execute(default);
        else if (parameter is T nullable) _execute(nullable);
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
