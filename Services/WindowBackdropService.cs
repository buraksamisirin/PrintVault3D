using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PrintVault3D.Services;

public static class WindowBackdropService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
    private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed Mica

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    public static void EnableMica(Window window, bool darkTheme = true)
    {
        if (Environment.OSVersion.Version.Build < 22000)
            return; // Not Windows 11

        window.Loaded += (s, e) =>
        {
            var source = PresentationSource.FromVisual(window) as HwndSource;
            if (source == null) return;

            var handle = source.Handle;

            // 1. Set Dark Mode
            int useDarkMode = darkTheme ? 1 : 0;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

            // 2. Set Mica Backdrop
            int backdropType = DWMSBT_MAINWINDOW;
            DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

            // 3. Remove background to let Mica show through
            window.Background = System.Windows.Media.Brushes.Transparent;

            // 4. Extend Frame (Optional, helps with borderless feel)
            // MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            // DwmExtendFrameIntoClientArea(handle, ref margins);
        };
    }

    public static void EnableAcrylic(Window window, bool darkTheme = true)
    {
        if (Environment.OSVersion.Version.Build < 22000)
            return; // Not Windows 11

        window.Loaded += (s, e) =>
        {
            var source = PresentationSource.FromVisual(window) as HwndSource;
            if (source == null) return;

            var handle = source.Handle;

            // 1. Set Dark Mode
            int useDarkMode = darkTheme ? 1 : 0;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));

            // 2. Set Acrylic Backdrop
            int backdropType = DWMSBT_TRANSIENTWINDOW;
            DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

            // 3. Remove background
            window.Background = System.Windows.Media.Brushes.Transparent;
        };
    }
}
