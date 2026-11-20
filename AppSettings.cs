using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LiveWallpaper
{
    public class AppSettings
    {
        public string? CurrentVideoPath { get; set; }
        public List<string> VideoLibrary { get; set; } = new();
        public bool IsMuted { get; set; } = true;
        public bool LoopInf { get; set; } = true;
        public int Volume { get; set; } = 40;
        public bool PauseOnFullscreen { get; set; } = true;
        public bool PauseOnLock { get; set; } = true;
        public bool PauseOnBattery { get; set; } = false;
        public int SelectedMonitorIndex { get; set; } = 0;
        public bool AutoStart { get; set; } = false;

        public static string SettingsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFilePath)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings.json; using defaults.", ex);
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings.json", ex);
            }
        }
    }
}
