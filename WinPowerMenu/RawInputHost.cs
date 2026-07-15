using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinPowerMenu;

/// <summary>
/// Registers for Raw Input on the HID collections that historically carry
/// power / sleep / wake button events, and reports every WM_INPUT to the
/// caller as (usagePage, usage, rawDataBytes). Also relays WM_POWERBROADCAST.
///
/// Attach to a Window (any WPF Window will do — its HwndSource is used).
/// This is the diagnostic path for machines where the physical power
/// button never reaches the low-level keyboard hook (ATX/ACPI style,
/// handhelds like ROG Ally, etc.).
/// </summary>
public sealed class RawInputHost : IDisposable
{
    private const int WM_INPUT = 0x00FF;
    private const int WM_POWERBROADCAST = 0x0218;

    private const uint RID_INPUT = 0x10000003;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const uint RIDEV_REMOVE = 0x00000001;

    private const uint RIM_TYPEMOUSE = 0;
    private const uint RIM_TYPEKEYBOARD = 1;
    private const uint RIM_TYPEHID = 2;

    private static readonly (ushort page, ushort usage)[] TargetCollections = new (ushort, ushort)[]
    {
        (0x01, 0x80), // Generic Desktop / System Control (Power Down 0x81, Sleep 0x82, Wake 0x83)
        (0x0C, 0x01), // Consumer / Consumer Control (Power 0x30, Sleep 0x32, etc.)
        (0x01, 0x06), // Generic Desktop / Keyboard — some HID power buttons masquerade as keys
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICELIST
    {
        public IntPtr hDevice;
        public uint dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWHID
    {
        public uint dwSizeHid;
        public uint dwCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO_HID
    {
        public uint dwVendorId;
        public uint dwProductId;
        public uint dwVersionNumber;
        public ushort usUsagePage;
        public ushort usUsage;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RID_DEVICE_INFO_UNION
    {
        [FieldOffset(0)] public RID_DEVICE_INFO_HID hid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RID_DEVICE_INFO
    {
        public uint cbSize;
        public uint dwType;
        public RID_DEVICE_INFO_UNION u;
    }

    private const uint RIDI_DEVICEINFO = 0x2000000b;
    private const uint RIDI_DEVICENAME = 0x20000007;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand,
        IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand,
        IntPtr pData, ref uint pcbSize);

    private HwndSource? _source;
    private bool _registered;

    /// <summary>Fired on UI thread for each raw HID event with (page, usage, data-bytes).</summary>
    public Action<ushort, ushort, byte[]>? OnHid { get; set; }

    /// <summary>Fired on UI thread for keyboard raw input with (vk, scan, flags).</summary>
    public Action<ushort, ushort, ushort>? OnKeyboard { get; set; }

    /// <summary>Fired on UI thread for WM_POWERBROADCAST with (wParam).</summary>
    public Action<int>? OnPowerBroadcast { get; set; }

    public void Attach(Window window)
    {
        var wih = new WindowInteropHelper(window);
        wih.EnsureHandle();
        var src = HwndSource.FromHwnd(wih.Handle)
            ?? throw new InvalidOperationException("HwndSource unavailable.");
        Attach(src);
    }

    public void Attach(HwndSource source)
    {
        if (_source != null) return;
        _source = source;
        _source.AddHook(WndProc);
        Register(_source.Handle);
    }

    private void Register(IntPtr hwnd)
    {
        var devs = new RAWINPUTDEVICE[TargetCollections.Length];
        for (int i = 0; i < TargetCollections.Length; i++)
        {
            devs[i] = new RAWINPUTDEVICE
            {
                usUsagePage = TargetCollections[i].page,
                usUsage = TargetCollections[i].usage,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = hwnd,
            };
        }
        _registered = RegisterRawInputDevices(devs, (uint)devs.Length,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        if (!_registered)
        {
            LearnLogger.Log($"RegisterRawInputDevices FAILED, error={Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        try
        {
            if (msg == WM_INPUT)
            {
                HandleInput(lParam);
            }
            else if (msg == WM_POWERBROADCAST)
            {
                OnPowerBroadcast?.Invoke(wParam.ToInt32());
            }
        }
        catch (Exception ex)
        {
            LearnLogger.Log("WndProc exception: " + ex.Message);
        }
        return IntPtr.Zero;
    }

    private void HandleInput(IntPtr hRawInput)
    {
        uint size = 0;
        GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size,
            (uint)Marshal.SizeOf<RAWINPUTHEADER>());
        if (size == 0) return;

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            uint got = GetRawInputData(hRawInput, RID_INPUT, buf, ref size,
                (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            if (got != size) return;

            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buf);
            IntPtr payload = IntPtr.Add(buf, Marshal.SizeOf<RAWINPUTHEADER>());

            if (header.dwType == RIM_TYPEHID)
            {
                var hid = Marshal.PtrToStructure<RAWHID>(payload);
                int dataLen = (int)(hid.dwSizeHid * hid.dwCount);
                if (dataLen <= 0 || dataLen > 4096) return;
                var data = new byte[dataLen];
                Marshal.Copy(IntPtr.Add(payload, Marshal.SizeOf<RAWHID>()), data, 0, dataLen);

                var (page, usage) = QueryHidUsage(header.hDevice);
                OnHid?.Invoke(page, usage, data);
            }
            else if (header.dwType == RIM_TYPEKEYBOARD)
            {
                var kb = Marshal.PtrToStructure<RAWKEYBOARD>(payload);
                OnKeyboard?.Invoke(kb.VKey, kb.MakeCode, kb.Flags);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static (ushort page, ushort usage) QueryHidUsage(IntPtr hDevice)
    {
        try
        {
            uint sz = (uint)Marshal.SizeOf<RID_DEVICE_INFO>();
            IntPtr p = Marshal.AllocHGlobal((int)sz);
            try
            {
                Marshal.WriteInt32(p, (int)sz); // cbSize
                if (GetRawInputDeviceInfo(hDevice, RIDI_DEVICEINFO, p, ref sz) > 0)
                {
                    var info = Marshal.PtrToStructure<RID_DEVICE_INFO>(p);
                    return (info.u.hid.usUsagePage, info.u.hid.usUsage);
                }
            }
            finally { Marshal.FreeHGlobal(p); }
        }
        catch { }
        return (0, 0);
    }

    public void Dispose()
    {
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
        if (_registered)
        {
            var devs = new RAWINPUTDEVICE[TargetCollections.Length];
            for (int i = 0; i < TargetCollections.Length; i++)
            {
                devs[i] = new RAWINPUTDEVICE
                {
                    usUsagePage = TargetCollections[i].page,
                    usUsage = TargetCollections[i].usage,
                    dwFlags = RIDEV_REMOVE,
                    hwndTarget = IntPtr.Zero,
                };
            }
            try { RegisterRawInputDevices(devs, (uint)devs.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()); }
            catch { }
            _registered = false;
        }
    }
}
