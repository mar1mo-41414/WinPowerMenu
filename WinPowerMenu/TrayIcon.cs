using System;
using System.Drawing;
using System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace WinPowerMenu;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayIcon(App app)
    {
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "WinPowerMenu",
        };

        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("電源メニューを表示");
        showItem.Click += (_, _) => app.ShowMenu();

        var settingsItem = new ToolStripMenuItem("設定…");
        settingsItem.Click += (_, _) => app.ShowSettings();

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => WpfApplication.Current.Shutdown();

        menu.Items.Add(showItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => app.ShowMenu();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
