using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WinPowerMenu;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstance;
    private LowLevelKeyboardHook? _hook;
    private HiddenRawInputWindow? _rawHost;
    private DisplayOffTrigger? _displayOff;
    private TrayIcon? _tray;
    private PowerMenuWindow? _menu;
    private AppSettings _settings = new();

    private void OnStartup(object sender, StartupEventArgs e)
    {
        InstallCrashHandlers();

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

        CrashLog.Info($"Startup OK, trigger={_settings.TriggerSource}");
    }

    private void InstallCrashHandlers()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            CrashLog.Write("DispatcherUnhandledException", e.Exception);
            e.Handled = true; // keep the app alive
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            CrashLog.Write("AppDomain.UnhandledException", e.ExceptionObject as Exception,
                $"IsTerminating={e.IsTerminating}");
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            CrashLog.Write("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _tray?.Dispose();
        _hook?.Dispose();
        _rawHost?.Dispose();
        _displayOff?.Dispose();
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
        _hook?.Dispose();       _hook = null;
        _rawHost?.Dispose();    _rawHost = null;
        _displayOff?.Dispose(); _displayOff = null;

        try
        {
            switch (_settings.TriggerSource)
            {
                case TriggerSource.Keyboard:
                    _hook = new LowLevelKeyboardHook
                    {
                        TargetVkCode = _settings.TriggerVkCode,
                        OnPowerKey = ShowMenu,
                    };
                    _hook.Start();
                    break;

                case TriggerSource.DisplayOff:
                    _displayOff = new DisplayOffTrigger(ShowMenu);
                    _displayOff.Start();
                    break;

                default: // HidSystemControl / HidConsumer / HidKeyboard
                    _rawHost = new HiddenRawInputWindow(_settings, ShowMenu);
                    _rawHost.Start();
                    break;
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
        try
        {
            var current = _menu;
            if (current is { IsVisible: true })
            {
                current.Activate();
                return;
            }

            var w = new PowerMenuWindow();
            _menu = w;
            w.Closed += (_, _) =>
            {
                if (ReferenceEquals(_menu, w)) _menu = null;
                ExecutionState.Release();
            };
            w.Show();
            w.Activate();
        }
        catch (Exception ex)
        {
            CrashLog.Write("ShowMenu", ex);
        }
    }

    public void ShowSettings()
    {
        var w = new SettingsWindow(_settings);
        w.ShowDialog();
        ApplyTrigger(); // apply any change made in settings
    }
}
