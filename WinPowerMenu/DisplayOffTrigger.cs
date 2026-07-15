using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinPowerMenu;

/// <summary>
/// Trigger source for machines where the physical power button never
/// reaches userland (ROG Ally with "Do nothing", most modern handhelds
/// with Modern Standby). Requires the Windows power-button action to be
/// set to "Turn off the display" (powercfg PBUTTONACTION = 4).
///
/// Flow: press power → OS turns off the console display → we receive
/// PBT_POWERSETTINGCHANGE(GUID_CONSOLE_DISPLAY_STATE = 0) → if the last
/// user input was recent (default: within 5 s) we assume it was the
/// button press rather than an idle timeout → wake the display and fire
/// the popup.
/// </summary>
public sealed class DisplayOffTrigger : IDisposable
{
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0;

    // Modern (Win8+), reliable on Modern Standby machines. Data: 0=off, 1=on, 2=dim.
    private static readonly Guid GUID_CONSOLE_DISPLAY_STATE =
        new("6fe69556-704a-47a0-8f24-c28d936fda47");
    // Older; still delivered on most desktops/laptops.
    private static readonly Guid GUID_MONITOR_POWER_ON =
        new("02731015-4510-4526-99e6-e5a17ebd1aea");

    [StructLayout(LayoutKind.Sequential)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(
        IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;

    private readonly Action _onTrigger;
    private readonly TimeSpan _recentInput;
    private readonly TimeSpan _debounce = TimeSpan.FromSeconds(2);

    private Window? _window;
    private HwndSource? _source;
    private IntPtr _hNotify1 = IntPtr.Zero;
    private IntPtr _hNotify2 = IntPtr.Zero;
    private DateTime _lastFire = DateTime.MinValue;

    public DisplayOffTrigger(Action onTrigger, TimeSpan? recentInput = null)
    {
        _onTrigger = onTrigger;
        _recentInput = recentInput ?? TimeSpan.FromSeconds(5);
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
            Title = "WinPowerMenu.DisplayOffHost",
        };
        _window.Show();
        _window.Hide();

        var wih = new WindowInteropHelper(_window);
        wih.EnsureHandle();
        _source = HwndSource.FromHwnd(wih.Handle);
        _source?.AddHook(WndProc);

        var g1 = GUID_CONSOLE_DISPLAY_STATE;
        _hNotify1 = RegisterPowerSettingNotification(wih.Handle, ref g1, DEVICE_NOTIFY_WINDOW_HANDLE);
        var g2 = GUID_MONITOR_POWER_ON;
        _hNotify2 = RegisterPowerSettingNotification(wih.Handle, ref g2, DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_POWERBROADCAST && wParam.ToInt32() == PBT_POWERSETTINGCHANGE)
        {
            var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
            if (setting.PowerSetting == GUID_CONSOLE_DISPLAY_STATE ||
                setting.PowerSetting == GUID_MONITOR_POWER_ON)
            {
                int dataOffset = Marshal.SizeOf<POWERBROADCAST_SETTING>();
                uint value = (uint)Marshal.ReadInt32(lParam, dataOffset);
                if (value == 0)
                {
                    OnDisplayOff();
                }
            }
        }
        return IntPtr.Zero;
    }

    private void OnDisplayOff()
    {
        if ((DateTime.UtcNow - _lastFire) < _debounce) return;

        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lii)) return;

        long deltaMs = unchecked((int)((uint)Environment.TickCount - lii.dwTime));
        if (deltaMs < 0 || deltaMs > _recentInput.TotalMilliseconds)
        {
            // stale input → this is likely an idle-timeout display-off, not a press
            return;
        }

        _lastFire = DateTime.UtcNow;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            WakeDisplay();
            _onTrigger();
        });
    }

    private static void WakeDisplay()
    {
        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
        PostMessage(HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)(-1));
    }

    public void Dispose()
    {
        if (_hNotify1 != IntPtr.Zero) { UnregisterPowerSettingNotification(_hNotify1); _hNotify1 = IntPtr.Zero; }
        if (_hNotify2 != IntPtr.Zero) { UnregisterPowerSettingNotification(_hNotify2); _hNotify2 = IntPtr.Zero; }
        if (_source != null) { _source.RemoveHook(WndProc); _source = null; }
        _window?.Close();
        _window = null;
        SetThreadExecutionState(ES_CONTINUOUS);
    }
}
