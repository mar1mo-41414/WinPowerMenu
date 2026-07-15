using System.Diagnostics;
using System.Text;

namespace WinPowerMenu;

/// <summary>
/// Thin wrapper around <c>powercfg.exe</c> for changing the Windows
/// physical-power-button action. Values follow the ButtonAction enum:
///   0 = Do nothing / no action
///   1 = Sleep
///   2 = Hibernate
///   3 = Shut down
///   4 = Turn off the display  ← the one WinPowerMenu wants for DisplayOff mode
/// </summary>
public enum PowerButtonAction
{
    Nothing = 0,
    Sleep = 1,
    Hibernate = 2,
    Shutdown = 3,
    TurnOffDisplay = 4,
}

public static class PowercfgHelper
{
    public static (bool ok, string output) SetPowerButtonAction(PowerButtonAction action)
    {
        var v = ((int)action).ToString();
        var sb = new StringBuilder();
        // Both AC and DC (battery), then activate the scheme so it takes effect.
        var steps = new[]
        {
            $"/SETACVALUEINDEX SCHEME_CURRENT SUB_BUTTONS PBUTTONACTION {v}",
            $"/SETDCVALUEINDEX SCHEME_CURRENT SUB_BUTTONS PBUTTONACTION {v}",
            "/S SCHEME_CURRENT",
        };
        foreach (var args in steps)
        {
            var (rc, so, se) = Run("powercfg.exe", args);
            sb.Append("> powercfg ").AppendLine(args);
            if (!string.IsNullOrWhiteSpace(so)) sb.AppendLine(so.TrimEnd());
            if (!string.IsNullOrWhiteSpace(se)) sb.AppendLine(se.TrimEnd());
            if (rc != 0)
            {
                sb.AppendLine($"(exit {rc})");
                return (false, sb.ToString());
            }
        }
        return (true, sb.ToString());
    }

    private static (int rc, string stdout, string stderr) Run(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        using var p = Process.Start(psi)!;
        var so = p.StandardOutput.ReadToEnd();
        var se = p.StandardError.ReadToEnd();
        p.WaitForExit(15_000);
        return (p.ExitCode, so, se);
    }
}
