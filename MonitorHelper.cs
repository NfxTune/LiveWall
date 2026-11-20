using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public class MonitorInfoDetail
    {
        public string DeviceName { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        public bool IsPrimary { get; set; }
        public uint DpiX { get; set; } = 96;
        public uint DpiY { get; set; } = 96;
        public double ScaleFactor => DpiX / 96.0;

        public override string ToString() =>
            $"{(IsPrimary ? "[Primary] " : "")}{DeviceName} | {Bounds.Width}x{Bounds.Height} at ({Bounds.X}, {Bounds.Y}) | Scale: {ScaleFactor * 100:F0}% ({DpiX} DPI)";
    }

    public static class MonitorHelper
    {
        private enum MonitorDpiType
        {
            MDT_EFFECTIVE_DPI = 0,
            MDT_ANGULAR_DPI = 1,
            MDT_RAW_DPI = 2,
            MDT_DEFAULT = MDT_EFFECTIVE_DPI
        }

        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        public static MonitorInfoDetail[] GetAllMonitors()
        {
            Screen[] screens = Screen.AllScreens;
            MonitorInfoDetail[] result = new MonitorInfoDetail[screens.Length];

            Logger.Info($"Querying display configuration: Found {screens.Length} monitor(s).");

            for (int i = 0; i < screens.Length; i++)
            {
                Screen scr = screens[i];
                MonitorInfoDetail detail = new MonitorInfoDetail
                {
                    DeviceName = scr.DeviceName,
                    Bounds = scr.Bounds,
                    WorkingArea = scr.WorkingArea,
                    IsPrimary = scr.Primary
                };

                // Lấy DPI cho từng màn hình
                try
                {
                    Point center = new Point(scr.Bounds.Left + scr.Bounds.Width / 2, scr.Bounds.Top + scr.Bounds.Height / 2);
                    IntPtr hMon = NativeMethods.MonitorFromWindow(IntPtr.Zero, NativeMethods.MONITOR_DEFAULTTONEAREST);

                    if (GetDpiForMonitor(hMon, MonitorDpiType.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                    {
                        detail.DpiX = dpiX;
                        detail.DpiY = dpiY;
                    }
                }
                catch
                {
                    detail.DpiX = 96;
                    detail.DpiY = 96;
                }

                result[i] = detail;
                Logger.Info($"  Monitor #{i + 1}: {detail}");
            }

            return result;
        }

        /// <summary>
        /// Tính toán hình chữ nhật bao phủ toàn bộ các màn hình (Virtual Screen bounds)
        /// </summary>
        public static Rectangle GetVirtualScreenBounds()
        {
            Rectangle totalBounds = SystemInformation.VirtualScreen;
            Logger.Info($"Virtual Screen total bounds: {totalBounds}");
            return totalBounds;
        }
    }
}
