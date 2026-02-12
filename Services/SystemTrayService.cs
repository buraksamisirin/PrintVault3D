using System.Drawing;
using System.Windows.Forms;

namespace PrintVault3D.Services;

/// <summary>
/// System tray integration service using Windows Forms NotifyIcon.
/// </summary>
public class SystemTrayService : ISystemTrayService
{
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed;
    private bool _initialized;

    public event EventHandler? RestoreRequested;
    public event EventHandler? ExitRequested;

    public bool IsVisible => _notifyIcon?.Visible ?? false;

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Create context menu
        _contextMenu = new ContextMenuStrip();
        
        var openItem = new ToolStripMenuItem("Open STLIE");
        openItem.Click += (s, e) => RestoreRequested?.Invoke(this, EventArgs.Empty);
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        
        var separatorItem = new ToolStripSeparator();
        
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(separatorItem);
        _contextMenu.Items.Add(exitItem);

        // Style the context menu with dark theme
        _contextMenu.BackColor = Color.FromArgb(22, 27, 34);
        _contextMenu.ForeColor = Color.FromArgb(240, 246, 252);
        _contextMenu.Renderer = new DarkMenuRenderer();

        // Create notify icon
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "STLIE",
            ContextMenuStrip = _contextMenu,
            Visible = false
        };

        _notifyIcon.MouseDoubleClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                RestoreRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void Show()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = true;
        }
    }

    public void Hide()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
        }
    }

    public void ShowBalloon(string title, string message, BalloonIconType iconType = BalloonIconType.Info)
    {
        if (_notifyIcon == null || !_notifyIcon.Visible)
            return;

        var tipIcon = iconType switch
        {
            BalloonIconType.Info => ToolTipIcon.Info,
            BalloonIconType.Warning => ToolTipIcon.Warning,
            BalloonIconType.Error => ToolTipIcon.Error,
            _ => ToolTipIcon.None
        };

        _notifyIcon.ShowBalloonTip(3000, title, message, tipIcon);
    }

    public void SetTooltip(string text)
    {
        if (_notifyIcon != null)
        {
            // Tooltip max length is 63 characters
            _notifyIcon.Text = text.Length > 63 ? text[..60] + "..." : text;
        }
    }

    private static Icon CreateDefaultIcon()
    {
        try 
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
               return Icon.ExtractAssociatedIcon(path) ?? SystemIcons.Application;
            }
        }
        catch 
        {
            // Fallback
        }
        
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        _contextMenu?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Custom renderer for dark themed context menu.
    /// </summary>
    private class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(240, 246, 252);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using var brush = new SolidBrush(Color.FromArgb(48, 54, 61));
                e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(22, 27, 34));
                e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
            }
        }
    }

    private class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(48, 54, 61);
        public override Color MenuItemBorder => Color.FromArgb(57, 208, 216);
        public override Color MenuItemSelected => Color.FromArgb(48, 54, 61);
        public override Color MenuStripGradientBegin => Color.FromArgb(22, 27, 34);
        public override Color MenuStripGradientEnd => Color.FromArgb(22, 27, 34);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(48, 54, 61);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(48, 54, 61);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(33, 38, 45);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(33, 38, 45);
        public override Color ToolStripDropDownBackground => Color.FromArgb(22, 27, 34);
        public override Color ImageMarginGradientBegin => Color.FromArgb(22, 27, 34);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(22, 27, 34);
        public override Color ImageMarginGradientEnd => Color.FromArgb(22, 27, 34);
        public override Color SeparatorDark => Color.FromArgb(48, 54, 61);
        public override Color SeparatorLight => Color.FromArgb(48, 54, 61);
    }
}
