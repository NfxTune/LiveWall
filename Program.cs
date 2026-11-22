using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LiveWallpaper
{
    internal static class Program
    {
        private static Mutex? _singleInstanceMutex;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindow(string? className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            if (args.Length >= 3 && args[0] == "--web-host")
            {
                int monitorIndex = args.Length >= 4 && int.TryParse(args[3], out int parsedIndex) ? parsedIndex : 0;
                WebWallpaperHost.Run(args[1], args[2], monitorIndex);
                return;
            }

            const string mutexName = "LiveWallpaperStudio_SingleInstanceMutex";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isOnlyInstance);
            if (!isOnlyInstance)
            {
                // Phát tín hiệu kích hoạt đến tiến trình đang chạy
                uint msgActivate = NativeMethods.RegisterWindowMessage("LiveWall_ActivateMainWindow");
                if (msgActivate != 0)
                {
                    NativeMethods.PostMessage((IntPtr)0xFFFF, msgActivate, IntPtr.Zero, IntPtr.Zero);
                }

                IntPtr existingWindow = FindWindow(null, "LiveWall");
                if (existingWindow != IntPtr.Zero)
                {
                    ShowWindow(existingWindow, 9 /* SW_RESTORE */);
                    SetForegroundWindow(existingWindow);
                }
                return;
            }

            Logger.Info("Starting LiveWallpaper Studio.");
            AppSettings settings = AppSettings.Load();

            if (args.Length > 0 && !args[0].StartsWith("--") && (File.Exists(args[0]) || args[0].StartsWith("http", StringComparison.OrdinalIgnoreCase)))
            {
                string source = args[0].StartsWith("http", StringComparison.OrdinalIgnoreCase) ? args[0] : Path.GetFullPath(args[0]);
                settings.CurrentVideoPath = source;
                if (!settings.VideoLibrary.Contains(source)) settings.VideoLibrary.Add(source);
                settings.Save();
            }

            string mpvDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libmpv-2.dll");
            if (!File.Exists(mpvDllPath))
            {
                MessageBox.Show($"Missing libmpv-2.dll in:\n{AppDomain.CurrentDomain.BaseDirectory}", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _singleInstanceMutex.ReleaseMutex();
                return;
            }

            DesktopManager desktopManager = new DesktopManager();
            if (!desktopManager.InitializeWorkerW())
            {
                MessageBox.Show("Could not initialize the desktop wallpaper host. See logs/app.log.", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _singleInstanceMutex.ReleaseMutex();
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => desktopManager.RestoreDesktop();
            using (TrayContext trayContext = new TrayContext(desktopManager, settings))
                Application.Run(trayContext);

            _singleInstanceMutex.ReleaseMutex();
        }
    }
}
