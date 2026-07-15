using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WinPowerMenu;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    // Learn-scoped resources
    private LowLevelKeyboardHook? _learnHook;
    private RawInputHost? _learnRaw;
    private DispatcherTimer? _learnTimer;

    // Pending candidates (staged, only saved on OK)
    private TriggerSource _pendingSource;
    private uint _pendingVk;
    private uint _pendingScan;
    private uint _pendingHidPage;
    private uint _pendingHidUsage;
    private string _pendingLabel;

    private bool _suppressAutoStartHandler;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        _pendingSource = settings.TriggerSource;
        _pendingVk = settings.TriggerVkCode;
        _pendingScan = settings.TriggerScanCode;
        _pendingHidPage = settings.TriggerHidUsagePage;
        _pendingHidUsage = settings.TriggerHidUsage;
        _pendingLabel = settings.TriggerLabel;

        RefreshKeyLabel();
        RefreshAutoStart();
    }

    private void RefreshKeyLabel()
    {
        string src = _pendingSource switch
        {
            TriggerSource.Keyboard         => "Keyboard (low-level hook)",
            TriggerSource.HidKeyboard      => "Raw Input (HID Keyboard)",
            TriggerSource.HidSystemControl => "Raw Input (HID System Control)",
            TriggerSource.HidConsumer      => "Raw Input (HID Consumer)",
            _ => _pendingSource.ToString(),
        };
        CurrentKeyLabel.Text =
            $"{_pendingLabel}\n" +
            $"Source: {src}  |  VK=0x{_pendingVk:X2}  Scan=0x{_pendingScan:X2}  " +
            $"HID Page=0x{_pendingHidPage:X2} Usage=0x{_pendingHidUsage:X2}";
    }

    private void RefreshAutoStart()
    {
        try
        {
            _suppressAutoStartHandler = true;
            AutoStartCheck.IsChecked = AutoStartManager.IsEnabled();
        }
        catch { }
        finally { _suppressAutoStartHandler = false; }
    }

    private void AutoStart_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressAutoStartHandler) return;
        try
        {
            AutoStartManager.SetEnabled(AutoStartCheck.IsChecked == true);
        }
        catch (Exception ex)
        {
            MessageBox.Show("自動起動の設定変更に失敗しました: " + ex.Message,
                "WinPowerMenu", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshAutoStart();
        }
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = LearnLogger.LogPath;
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "(no learn sessions yet)\n");
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("ログを開けませんでした: " + ex.Message,
                "WinPowerMenu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Learn_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "キー / ボタンを押してください… (最大 10 秒)";
        LearnButton.IsEnabled = false;

        LearnLogger.StartSession($"machine={Environment.MachineName}, user={Environment.UserName}");

        // 1) Low-level keyboard hook (VK path).
        _learnHook = new LowLevelKeyboardHook
        {
            LearnMode = true,
            OnKeyLearned = (vk, scan) =>
            {
                LearnLogger.Log($"LL keyboard hook: VK=0x{vk:X2} Scan=0x{scan:X2}");
                AcceptCandidate(TriggerSource.Keyboard,
                    vk: vk, scan: scan,
                    label: $"Learned VK 0x{vk:X2} (low-level hook)");
            },
        };
        try { _learnHook.Start(); }
        catch (Exception ex)
        {
            LearnLogger.Log("LL hook start failed: " + ex.Message);
        }

        // 2) Raw Input (HID + HID keyboard) + WM_POWERBROADCAST.
        _learnRaw = new RawInputHost
        {
            OnHid = (page, usage, data) =>
            {
                LearnLogger.Log(
                    $"RawInput HID page=0x{page:X2} usage=0x{usage:X2} bytes=[{LearnLogger.HexDump(data)}]");
                if (AnyNonZero(data))
                {
                    var pageName = HidUsageName(page, usage);
                    AcceptCandidate(
                        page == 0x0C ? TriggerSource.HidConsumer : TriggerSource.HidSystemControl,
                        hidPage: page, hidUsage: usage,
                        label: $"HID {pageName} (page 0x{page:X2}, usage 0x{usage:X2})");
                }
            },
            OnKeyboard = (vk, scan, flags) =>
            {
                LearnLogger.Log(
                    $"RawInput Keyboard VK=0x{vk:X2} Scan=0x{scan:X2} Flags=0x{flags:X4}");
                if ((flags & 0x01) != 0) return; // key-break; ignore
                AcceptCandidate(TriggerSource.HidKeyboard,
                    vk: vk, scan: scan,
                    label: $"Learned VK 0x{vk:X2} (Raw Input keyboard)");
            },
            OnPowerBroadcast = (w) =>
            {
                LearnLogger.Log($"WM_POWERBROADCAST wParam=0x{w:X4}");
            },
        };
        _learnRaw.Attach(this);

        _learnTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _learnTimer.Tick += (_, _) =>
        {
            FinishLearn();
            if (StatusText.Text.StartsWith("キー"))
            {
                StatusText.Text =
                    "タイムアウトしました。何も検出できません。\n" +
                    "→「learn.log を開く」ボタンでログを送ってください。";
            }
        };
        _learnTimer.Start();
    }

    private void AcceptCandidate(TriggerSource source,
        uint vk = 0, uint scan = 0,
        uint hidPage = 0, uint hidUsage = 0,
        string label = "")
    {
        // First-match wins (learn mode should end on the first real event).
        if (_learnHook is null && _learnRaw is null) return;

        _pendingSource = source;
        _pendingVk = vk;
        _pendingScan = scan;
        _pendingHidPage = hidPage;
        _pendingHidUsage = hidUsage;
        _pendingLabel = label;

        FinishLearn();
        RefreshKeyLabel();
        StatusText.Text = "登録候補にセットしました。OK で保存します。";
    }

    private void FinishLearn()
    {
        _learnTimer?.Stop();
        _learnTimer = null;

        if (_learnHook != null)
        {
            _learnHook.LearnMode = false;
            _learnHook.Dispose();
            _learnHook = null;
        }
        if (_learnRaw != null)
        {
            _learnRaw.Dispose();
            _learnRaw = null;
        }
        LearnButton.IsEnabled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _settings.TriggerSource = _pendingSource;
        _settings.TriggerVkCode = _pendingVk;
        _settings.TriggerScanCode = _pendingScan;
        _settings.TriggerHidUsagePage = _pendingHidPage;
        _settings.TriggerHidUsage = _pendingHidUsage;
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
        FinishLearn();
        DialogResult = false;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        FinishLearn();
        base.OnClosed(e);
    }

    private static bool AnyNonZero(byte[] data)
    {
        for (int i = 1; i < data.Length; i++)
            if (data[i] != 0) return true;
        return data.Length > 0 && data[0] != 0;
    }

    private static string HidUsageName(ushort page, ushort usage)
    {
        return (page, usage) switch
        {
            (0x01, 0x80) => "System Control (collection)",
            (0x01, 0x81) => "System Power Down",
            (0x01, 0x82) => "System Sleep",
            (0x01, 0x83) => "System Wake Up",
            (0x01, 0x06) => "Keyboard (collection)",
            (0x0C, 0x01) => "Consumer Control (collection)",
            (0x0C, 0x30) => "Consumer Power",
            (0x0C, 0x32) => "Consumer Sleep",
            _ => "unknown"
        };
    }
}
