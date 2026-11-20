using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public static class ShortcutManager
    {
        public static bool TryCreateStartMenuShortcut(out string shortcutPath)
        {
            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            shortcutPath = Path.Combine(startMenu, "Programs", "LiveWall.lnk");

            object? shellObject = null;
            object? shortcutObject = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
                Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                    ?? throw new InvalidOperationException("Windows Script Host is not available.");

                shellObject = Activator.CreateInstance(shellType)
                    ?? throw new InvalidOperationException("Could not create the Windows Script Host object.");
                dynamic shell = shellObject;
                shortcutObject = shell.CreateShortcut(shortcutPath);
                dynamic shortcut = shortcutObject;
                shortcut.TargetPath = Application.ExecutablePath;
                shortcut.WorkingDirectory = AppContext.BaseDirectory;
                shortcut.Description = "Mở LiveWall để quản lý hình nền động.";
                shortcut.IconLocation = $"{Application.ExecutablePath},0";
                shortcut.Save();
                Logger.Info($"Created Start Menu shortcut: '{shortcutPath}'");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create Start Menu shortcut", ex);
                return false;
            }
            finally
            {
                if (shortcutObject is not null && Marshal.IsComObject(shortcutObject))
                    Marshal.FinalReleaseComObject(shortcutObject);
                if (shellObject is not null && Marshal.IsComObject(shellObject))
                    Marshal.FinalReleaseComObject(shellObject);
            }
        }
    }
}
