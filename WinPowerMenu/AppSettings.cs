using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinPowerMenu;

public enum TriggerSource
{
    Keyboard = 0,        // low-level keyboard hook, VK match
    HidSystemControl = 1,// Raw Input on HID Generic Desktop / System Control (0x01/0x80)
    HidConsumer = 2,     // Raw Input on HID Consumer (0x0C/0x01)
    HidKeyboard = 3,     // Raw Input keyboard-type, VK match (bypasses low-level hook)
}

public sealed class AppSettings
{
    // Which input surface the trigger comes from.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TriggerSource TriggerSource { get; set; } = TriggerSource.Keyboard;

    // Keyboard / HidKeyboard trigger: VK_SLEEP by default.
    public uint TriggerVkCode { get; set; } = 0x5F;
    public uint TriggerScanCode { get; set; } = 0;

    // HidSystemControl / HidConsumer trigger: the HID usage that fires the popup.
    public uint TriggerHidUsagePage { get; set; } = 0x01;
    public uint TriggerHidUsage { get; set; } = 0x81; // System Power Down

    public string TriggerLabel { get; set; } = "VK_SLEEP (0x5F)";

    // Show the first-launch prompt only once.
    public bool FirstLaunchDone { get; set; } = false;

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
