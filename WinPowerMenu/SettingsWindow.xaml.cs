using System;
using System.Windows;
using System.Windows.Threading;

namespace WinPowerMenu;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly LowLevelKeyboardHook _hook;
    private DispatcherTimer? _timeout;

    private uint _pendingVk;
    private uint _pendingScan;
    private string _pendingLabel;

    public SettingsWindow(AppSettings settings, LowLevelKeyboardHook hook)
    {
        InitializeComponent();
        _settings = settings;
        _hook = hook;
        _pendingVk = settings.TriggerVkCode;
        _pendingScan = settings.TriggerScanCode;
        _pendingLabel = settings.TriggerLabel;
        Refresh();
    }

    private void Refresh()
    {
        CurrentKeyLabel.Text = $"{_pendingLabel}  (VK=0x{_pendingVk:X2}, Scan=0x{_pendingScan:X2})";
    }

    private void Learn_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "キーを押してください… (10 秒以内)";
        LearnButton.IsEnabled = false;

        _hook.OnKeyLearned = (vk, scan) =>
        {
            StopTimeout();
            _pendingVk = vk;
            _pendingScan = scan;
            _pendingLabel = LookupName(vk);
            Refresh();
            StatusText.Text = "登録候補にセットしました。OK で保存します。";
            LearnButton.IsEnabled = true;
        };
        _hook.LearnMode = true;

        _timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timeout.Tick += (_, _) =>
        {
            StopTimeout();
            if (_hook.LearnMode)
            {
                _hook.LearnMode = false;
                StatusText.Text = "タイムアウトしました。";
                LearnButton.IsEnabled = true;
            }
        };
        _timeout.Start();
    }

    private void StopTimeout()
    {
        _timeout?.Stop();
        _timeout = null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _settings.TriggerVkCode = _pendingVk;
        _settings.TriggerScanCode = _pendingScan;
        _settings.TriggerLabel = _pendingLabel;
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show("設定の保存に失敗しました: " + ex.Message,
                "WinPowerMenu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _hook.LearnMode = false;
        StopTimeout();
        DialogResult = false;
        Close();
    }

    private static string LookupName(uint vk) => vk switch
    {
        0x5F => "VK_SLEEP (0x5F)",
        0xB5 => "VK_LAUNCH_APP1 (0xB5)",
        0xB6 => "VK_LAUNCH_APP2 (0xB6)",
        0xB0 => "VK_MEDIA_NEXT (0xB0)",
        0xB1 => "VK_MEDIA_PREV (0xB1)",
        0xB2 => "VK_MEDIA_STOP (0xB2)",
        0xB3 => "VK_MEDIA_PLAY_PAUSE (0xB3)",
        _    => $"VK 0x{vk:X2}"
    };
}
