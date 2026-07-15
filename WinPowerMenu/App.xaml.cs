using System;
using System.Threading;
using System.Windows;

namespace WinPowerMenu;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstance;
    private LowLevelKeyboardHook? _hook;
    private TrayIcon? _tray;
    private PowerMenuWindow? _menu;
    private AppSettings _settings = new();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "WinPowerMenu.SingleInstance.v1", out bool created);
        if (!created)
        {
            Shutdown();
            return;
        }

        _settings = AppSettings.Load();

        _hook = new LowLevelKeyboardHook
        {
            TargetVkCode = _settings.TriggerVkCode,
            OnPowerKey = ShowMenu,
        };

        try
        {
            _hook.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "低レベルキーボードフックの登録に失敗しました。\n" + ex.Message,
                "WinPowerMenu", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        _tray = new TrayIcon(this);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _tray?.Dispose();
        _hook?.Dispose();
        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();
    }

    public AppSettings Settings => _settings;

    public LowLevelKeyboardHook Hook => _hook!;

    public void ShowMenu()
    {
        if (_menu is { IsVisible: true })
        {
            _menu.Activate();
            return;
        }
        _menu = new PowerMenuWindow();
        _menu.Closed += (_, _) => _menu = null;
        _menu.Show();
        _menu.Activate();
    }

    public void ShowSettings()
    {
        var w = new SettingsWindow(_settings, _hook!);
        w.ShowDialog();
        _hook!.TargetVkCode = _settings.TriggerVkCode;
    }
}
