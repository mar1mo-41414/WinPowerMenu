using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinPowerMenu;

public static class PowerActions
{
    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public static void Shutdown() => Run("shutdown.exe", "/s /t 0");

    public static void Restart() => Run("shutdown.exe", "/r /t 0");

    public static void Sleep() => SetSuspendState(false, false, false);

    private static void Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        Process.Start(psi);
    }
}
