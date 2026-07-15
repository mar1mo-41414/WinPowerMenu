using System;
using System.IO;
using System.Text;

namespace WinPowerMenu;

/// <summary>
/// Appends unhandled exceptions to
/// %LOCALAPPDATA%\WinPowerMenu\crash.log — the app installs global
/// exception handlers in <see cref="App.OnStartup"/> so a second-press
/// crash doesn't vanish silently.
/// </summary>
public static class CrashLog
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinPowerMenu", "crash.log");

    public static void Write(string source, Exception? ex, string? extra = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var sb = new StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ")
              .AppendLine(source);
            if (extra != null) sb.AppendLine(extra);
            if (ex != null) sb.AppendLine(ex.ToString());
            sb.AppendLine();
            File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // logging failure is not fatal
        }
    }

    public static void Info(string line) => Write("INFO", null, line);
}
