using CommunityToolkit.Mvvm.ComponentModel;

namespace PrintVault3D.ViewModels;

/// <summary>
/// Base class for all ViewModels providing common MVVM functionality.
/// Implements IDisposable for proper cleanup of event subscriptions.
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private bool _isBusy;
    private string? _busyMessage;
    private bool _disposed;

    /// <summary>
    /// Indicates whether the ViewModel is currently processing.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Message to display while busy.
    /// </summary>
    public string? BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    /// <summary>
    /// Sets the busy state with an optional message.
    /// </summary>
    protected void SetBusy(bool isBusy, string? message = null)
    {
        IsBusy = isBusy;
        BusyMessage = message;
    }

    /// <summary>
    /// Executes an async operation while showing busy state.
    /// </summary>
    protected async Task ExecuteBusyAsync(Func<Task> operation, string? busyMessage = null)
    {
        try
        {
            SetBusy(true, busyMessage);
            await operation();
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Executes an async operation while showing busy state and returns a result.
    /// </summary>
    protected async Task<T?> ExecuteBusyAsync<T>(Func<Task<T>> operation, string? busyMessage = null)
    {
        try
        {
            SetBusy(true, busyMessage);
            return await operation();
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Safely executes an action on the UI dispatcher thread.
    /// Handles the case where Application.Current may be null during shutdown.
    /// </summary>
    protected void SafeDispatcher(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.Invoke(action);
        }
    }

    /// <summary>
    /// Safely executes an async action on the UI dispatcher thread.
    /// </summary>
    protected async Task SafeDispatcherAsync(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app == null) return;

        await app.Dispatcher.InvokeAsync(action);
    }

    /// <summary>
    /// Override in derived classes to dispose managed resources.
    /// </summary>
    protected virtual void OnDispose()
    {
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        OnDispose();
        GC.SuppressFinalize(this);
    }
}

