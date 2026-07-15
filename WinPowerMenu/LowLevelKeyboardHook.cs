using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace WinPowerMenu;

/// <summary>
/// Low-level keyboard hook that only consumes the configured "power key".
/// All other keys are passed through untouched, so other tools
/// (AutoHotkey, PowerToys, etc.) keep working.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private readonly HookProc _proc;
    private IntPtr _hookId = IntPtr.Zero;

    /// <summary>VK code to listen for. Only this key is consumed.</summary>
    public uint? TargetVkCode { get; set; }

    /// <summary>Fired (on UI thread) when the target key is pressed.</summary>
    public Action? OnPowerKey { get; set; }

    /// <summary>When true, the next key press is captured and reported via <see cref="OnKeyLearned"/>.</summary>
    public bool LearnMode { get; set; }

    /// <summary>Fired (on UI thread) when a key is learned. Args: (vkCode, scanCode).</summary>
    public Action<uint, uint>? OnKeyLearned { get; set; }

    public LowLevelKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        using var proc = Process.GetCurrentProcess();
        using var mod = proc.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(mod.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"SetWindowsHookEx failed (error {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                if (LearnMode)
                {
                    LearnMode = false;
                    var cb = OnKeyLearned;
                    if (cb != null)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(
                            () => cb(data.vkCode, data.scanCode));
                    }
                    return (IntPtr)1; // swallow so it doesn't act on the app underneath
                }

                if (TargetVkCode.HasValue && data.vkCode == TargetVkCode.Value)
                {
                    var cb = OnPowerKey;
                    if (cb != null)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(cb);
                    }
                    return (IntPtr)1; // consume — do NOT forward to other apps
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
