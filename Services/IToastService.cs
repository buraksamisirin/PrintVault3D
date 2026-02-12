namespace PrintVault3D.Services;

/// <summary>
/// Toast notification types.
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Toast notification event args.
/// </summary>
public class ToastEventArgs : EventArgs
{
    public string Message { get; }
    public ToastType Type { get; }
    public int DurationMs { get; }

    public ToastEventArgs(string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        Message = message;
        Type = type;
        DurationMs = durationMs;
    }
}

/// <summary>
/// Service interface for toast notifications.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Event raised when a toast should be shown.
    /// </summary>
    event EventHandler<ToastEventArgs>? ToastRequested;

    /// <summary>
    /// Shows an info toast.
    /// </summary>
    void ShowInfo(string message, int durationMs = 3000);

    /// <summary>
    /// Shows a success toast.
    /// </summary>
    void ShowSuccess(string message, int durationMs = 3000);

    /// <summary>
    /// Shows a warning toast.
    /// </summary>
    void ShowWarning(string message, int durationMs = 3000);

    /// <summary>
    /// Shows an error toast.
    /// </summary>
    void ShowError(string message, int durationMs = 5000);
}

