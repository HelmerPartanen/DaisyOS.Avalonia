using System.Windows.Input;

namespace DaisyOS.Core.Helpers;

public sealed class AsyncRelayCommand : ICommand, IDisposable
{
    private readonly Func<object?, CancellationToken, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private CancellationTokenSource? _executionCts;
    private bool _isRunning;
    private bool _disposed;

    public AsyncRelayCommand(Func<Task> execute)
        : this((_, _) => execute())
    {
    }

    public AsyncRelayCommand(Func<CancellationToken, Task> execute)
        : this((_, cancellationToken) => execute(cancellationToken))
    {
    }

    public AsyncRelayCommand(
        Func<object?, CancellationToken, Task> execute,
        Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public event EventHandler<Exception>? ExecutionFailed;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value)
            {
                return;
            }

            _isRunning = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CanExecute(object? parameter) =>
        !_disposed && !IsRunning && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _executionCts = new CancellationTokenSource();
        IsRunning = true;
        try
        {
            await _execute(parameter, _executionCts.Token);
        }
        catch (OperationCanceledException) when (_executionCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ExecutionFailed?.Invoke(this, ex);
        }
        finally
        {
            _executionCts.Dispose();
            _executionCts = null;
            IsRunning = false;
        }
    }

    public void Cancel() => _executionCts?.Cancel();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
