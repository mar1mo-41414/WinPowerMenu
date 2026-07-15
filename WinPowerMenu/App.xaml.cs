using System;
using System.Threading;
using System.Windows;

namespace WinPowerMenu;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstance;
    private LowLevelKeyboardHook? _hook;
    private HiddenRawInputWindow? _rawHost;
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
        MaybeShowFirstLaunchPrompt();
        ApplyTrigger();
        _tray = new TrayIcon(this);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _tray?.Dispose();
        _hook?.Dispose();
        _rawHost?.Dispose();
        try { _singleInstance?.ReleaseMutex(); } catch { }
        _singleInstance?.Dispose();
    }

    public AppSettings Settings => _settings;
    public LowLevelKeyboardHook? Hook => _hook;

    private void MaybeShowFirstLaunchPrompt()
    {
        if (_settings.FirstLaunchDone) return;
        _settings.FirstLaunchDone = true;

        var result = MessageBox.Show(
            "Windows へのサインイン時に WinPowerMenu を自動起動しますか？\n\n" +
            "（後から「設定」で切り替えできます）",
            "WinPowerMenu 初回設定",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        try
        {
            AutoStartManager.SetEnabled(result == MessageBoxResult.Yes);
        }
        catch (Exception ex)
        {
            MessageBox.Show("自動起動の登録に失敗しました: " + ex.Message,
                "WinPowerMenu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        try { _settings.Save(); } catch { }
    }

    /// <summary>Rebuild the runtime input capture path from current settings.</summary>
    public void ApplyTrigger()
    {
        _hook?.Dispose();
        _hook = null;
        _rawHost?.Dispose();
        _rawHost = null;

        try
        {
            if (_settings.TriggerSource == TriggerSource.Keyboard)
            {
                _hook = new LowLevelKeyboardHook
                {
                    TargetVkCode = _settings.TriggerVkCode,
                    OnPowerKey = ShowMenu,
                };
                _hook.Start();
            }
            else
            {
                _rawHost = new HiddenRawInputWindow(_settings, ShowMenu);
                _rawHost.Start();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "トリガー入力の登録に失敗しました。\n" + ex.Message,
                "WinPowerMenu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

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
        var w = new SettingsWindow(_settings);
        w.ShowDialog();
        ApplyTrigger(); // apply any change made in settings
    }
}
