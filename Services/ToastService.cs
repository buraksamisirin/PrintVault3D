namespace PrintVault3D.Services;

/// <summary>
/// Simple toast notification service.
/// </summary>
public class ToastService : IToastService
{
    public event EventHandler<ToastEventArgs>? ToastRequested;

    public void ShowInfo(string message, int durationMs = 3000)
    {
        ToastRequested?.Invoke(this, new ToastEventArgs(message, ToastType.Info, durationMs));
    }

    public void ShowSuccess(string message, int durationMs = 3000)
    {
        ToastRequested?.Invoke(this, new ToastEventArgs(message, ToastType.Success, durationMs));
    }

    public void ShowWarning(string message, int durationMs = 3000)
    {
        ToastRequested?.Invoke(this, new ToastEventArgs(message, ToastType.Warning, durationMs));
    }

    public void ShowError(string message, int durationMs = 5000)
    {
        ToastRequested?.Invoke(this, new ToastEventArgs(message, ToastType.Error, durationMs));
    }
}

