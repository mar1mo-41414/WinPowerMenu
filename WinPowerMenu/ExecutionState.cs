using System.Runtime.InteropServices;

namespace WinPowerMenu;

/// <summary>
/// Small wrapper around SetThreadExecutionState so DisplayOffTrigger and
/// App agree on the flags. Call <see cref="KeepDisplayOn"/> when the
/// popup is about to be visible, <see cref="Release"/> when it closes,
/// so the machine can idle-sleep normally afterwards.
/// </summary>
public static class ExecutionState
{
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS       = 0x80000000;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_SYSTEM_REQUIRED  = 0x00000001;

    public static void KeepDisplayOn() =>
        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);

    public static void Release() =>
        SetThreadExecutionState(ES_CONTINUOUS);
}
