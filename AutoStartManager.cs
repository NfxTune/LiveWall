using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LiveWallpaper
{
    public static class AutoStartManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "LiveWallpaperStudio";

        public static bool IsEnabled()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check autostart registry key", ex);
                return false;
            }
        }

        public static bool SetAutoStart(bool enable)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null)
                {
                    Logger.Error("Failed to open HKCU Run registry key for writing.");
                    return false;
                }

                if (enable)
                {
                    string exePath = Application.ExecutablePath;
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    Logger.Info($"Registered LiveWallpaper in Windows Startup Registry: '{exePath}'");
                }
                else
                {
                    if (key.GetValue(AppName) != null)
                    {
                        key.DeleteValue(AppName, false);
                        Logger.Info("Removed LiveWallpaper from Windows Startup Registry.");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to modify autostart registry key (enable={enable})", ex);
                return false;
            }
        }
    }
}
