using System;
using System.Windows;
using System.Windows.Interop;

namespace WinPowerMenu;

/// <summary>
/// A zero-sized invisible WPF window that owns the RawInputHost when the
/// trigger source is HID. WPF Raw Input requires an HWND; a message-only
/// window (HWND_MESSAGE) can be added later if needed, but a hidden Window
/// with WS_EX_TOOLWINDOW + no chrome + Visibility=Hidden is enough and
/// doesn't appear in Alt+Tab / taskbar.
/// </summary>
public sealed class HiddenRawInputWindow : IDisposable
{
    private readonly AppSettings _settings;
    private readonly Action _onTrigger;
    private Window? _window;
    private RawInputHost? _host;

    public HiddenRawInputWindow(AppSettings settings, Action onTrigger)
    {
        _settings = settings;
        _onTrigger = onTrigger;
    }

    public void Start()
    {
        _window = new Window
        {
            Width = 0, Height = 0,
            Left = -32000, Top = -32000,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden,
            AllowsTransparency = false,
            Title = "WinPowerMenu.Hidden",
        };
        _window.Show();
        _window.Hide();

        _host = new RawInputHost
        {
            OnHid = (page, usage, data) =>
            {
                if (Matches(page, usage, data))
                    _onTrigger();
            },
            OnKeyboard = (vk, scan, flags) =>
            {
                if (_settings.TriggerSource == TriggerSource.HidKeyboard
                    && vk == _settings.TriggerVkCode
                    && (flags & 0x01) == 0) // key-make (not break)
                {
                    _onTrigger();
                }
            },
        };
        _host.Attach(_window);
    }

    private bool Matches(ushort page, ushort usage, byte[] data)
    {
        // Only fire if this HID event came from the collection we care about
        // and at least one byte is non-zero (button pressed / non-idle state).
        var expectedPage = (ushort)_settings.TriggerHidUsagePage;
        var expectedUsage = (ushort)_settings.TriggerHidUsage;

        if (_settings.TriggerSource == TriggerSource.HidSystemControl
            && page == 0x01 && (expectedPage == 0 || page == expectedPage))
        {
            return HasAnyNonZero(data);
        }
        if (_settings.TriggerSource == TriggerSource.HidConsumer
            && page == 0x0C && (expectedPage == 0 || page == expectedPage))
        {
            return HasAnyNonZero(data);
        }
        return false;
    }

    private static bool HasAnyNonZero(byte[] data)
    {
        // First byte is often the report ID; check the remaining bytes.
        for (int i = 1; i < data.Length; i++)
            if (data[i] != 0) return true;
        return data.Length > 0 && data[0] != 0;
    }

    public void Dispose()
    {
        _host?.Dispose();
        _host = null;
        _window?.Close();
        _window = null;
    }
}
