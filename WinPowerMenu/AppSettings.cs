using System;
using System.IO;
using System.Text.Json;

namespace WinPowerMenu;

public sealed class AppSettings
{
    // VK_SLEEP = 0x5F, common default for a dedicated sleep/power key.
    public uint TriggerVkCode { get; set; } = 0x5F;
    public uint TriggerScanCode { get; set; } = 0;
    public string TriggerLabel { get; set; } = "VK_SLEEP (0x5F)";

    public static string SettingsPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinPowerMenu");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s != null) return s;
            }
        }
        catch
        {
            // fall through to defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
