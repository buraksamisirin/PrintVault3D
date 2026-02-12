using System.Windows;

namespace PrintVault3D.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
        });
    }
}
