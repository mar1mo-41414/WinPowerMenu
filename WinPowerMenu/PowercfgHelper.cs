using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Enumerate every power scheme registered with Windows and force
    /// PBUTTONACTION to <paramref name="action"/> on both AC and DC.
    /// Defensive against OEM software (e.g. ASUS Armoury Crate) that
    /// switches schemes at runtime — otherwise one scheme having
    /// PBUTTONACTION=3 (shutdown) means an accidental shutdown the
    /// next time the profile flips. Overlay / dynamic schemes that
    /// reject writes are skipped silently.
    /// </summary>
    public static (bool ok, int touched, string log) EnforcePowerButtonActionAllSchemes(PowerButtonAction action)
    {
        var sb = new StringBuilder();
        var (rc, list, err) = Run("powercfg.exe", "/L");
        if (rc != 0)
        {
            sb.AppendLine("powercfg /L failed: " + err);
            return (false, 0, sb.ToString());
        }

        var ids = new List<string>();
        foreach (Match m in Regex.Matches(list, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"))
        {
            ids.Add(m.Value);
        }
        if (ids.Count == 0)
        {
            sb.AppendLine("no schemes parsed");
            return (false, 0, sb.ToString());
        }

        var v = ((int)action).ToString();
        int touched = 0;
        foreach (var id in ids)
        {
            var (r1, _, e1) = Run("powercfg.exe", $"/SETACVALUEINDEX {id} SUB_BUTTONS PBUTTONACTION {v}");
            var (r2, _, e2) = Run("powercfg.exe", $"/SETDCVALUEINDEX {id} SUB_BUTTONS PBUTTONACTION {v}");
            if (r1 == 0 && r2 == 0) touched++;
            else
            {
                sb.Append("scheme ").Append(id).AppendLine(" partially skipped (likely overlay)");
                if (!string.IsNullOrWhiteSpace(e1)) sb.AppendLine("  " + e1.TrimEnd());
                if (!string.IsNullOrWhiteSpace(e2)) sb.AppendLine("  " + e2.TrimEnd());
            }
        }
        Run("powercfg.exe", "/S SCHEME_CURRENT");
        return (true, touched, sb.ToString());
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
