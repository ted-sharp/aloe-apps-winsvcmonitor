using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aloe.Apps.WindowsServiceMonitorClient.Services;

public class TrayIconManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? OpenServicesRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager()
    {
        this._notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "サービス監視 - 正常"
        };

        this._notifyIcon.DoubleClick += this.NotifyIcon_DoubleClick;
        this._notifyIcon.ContextMenuStrip = this.CreateContextMenu();

        this.SetStatusGreen();
    }

    public void SetStatusGreen()
    {
        this._notifyIcon.Icon = this.CreateIcon(Color.Green);
        this._notifyIcon.Text = "サービス監視 - 正常";
    }

    public void SetStatusRed()
    {
        this._notifyIcon.Icon = this.CreateIcon(Color.Red);
        this._notifyIcon.Text = "サービス監視 - 必須サービス停止";
    }

    private Icon CreateIcon(Color color)
    {
        // 古いアイコンのHICONを解放
        var oldIcon = this._notifyIcon.Icon;

        Icon newIcon;
        using (var bitmap = new Bitmap(16, 16))
        {
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, 0, 0, 16, 16);
                }
            }
            var hIcon = bitmap.GetHicon();
            newIcon = Icon.FromHandle(hIcon);
        }

        if (oldIcon != null)
        {
            DestroyIcon(oldIcon.Handle);
            oldIcon.Dispose();
        }

        return newIcon;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showMenuItem = new ToolStripMenuItem("表示/非表示");
        showMenuItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(showMenuItem);

        var servicesMenuItem = new ToolStripMenuItem("ローカルのサービス管理を開く");
        servicesMenuItem.Click += (s, e) => OpenServicesRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(servicesMenuItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("終了");
        exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitMenuItem);

        return menu;
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (this._disposed) return;

        this._notifyIcon.Visible = false;
        this._notifyIcon.Dispose();
        this._disposed = true;
    }
}
