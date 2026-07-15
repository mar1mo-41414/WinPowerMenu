using System;
using System.IO;
using System.Text;

namespace WinPowerMenu;

/// <summary>
/// Appends timestamped diagnostic lines to
/// %LOCALAPPDATA%\WinPowerMenu\learn.log so we can see which OS surface
/// the physical power button actually reaches on a given machine.
/// </summary>
public static class LearnLogger
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinPowerMenu", "learn.log");

    public static void StartSession(string note)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"\n===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} learn session: {note} =====\n",
                Encoding.UTF8);
        }
        catch
        {
            // logging failure is not fatal
        }
    }

    public static void Log(string line)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:HH:mm:ss.fff}] {line}\n",
                Encoding.UTF8);
        }
        catch { }
    }

    public static string HexDump(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 3);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }
}
