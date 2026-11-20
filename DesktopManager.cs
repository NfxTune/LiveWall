using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace LiveWallpaper
{
    /// <summary>
    /// Quản lý việc hook WallpaperForm vào lớp WorkerW của Windows Desktop.
    /// Hỗ trợ Windows 10, Windows 11, và Windows 11 24H2 (Build 26100+).
    /// </summary>
    public class DesktopManager
    {
        private static NativeMethods.EnumWindowsProc? _enumWindowsCallback;

        private IntPtr _workerWHwnd = IntPtr.Zero;
        private IntPtr _attachedHwnd = IntPtr.Zero;

        public IntPtr WorkerWHwnd => _workerWHwnd;

        /// <summary>
        /// Tìm chính xác cửa sổ WorkerW nằm ngay phía sau SHELLDLL_DefView trong Z-order.
        /// Trên Windows 11 24H2: SHELLDLL_DefView nằm trong Progman, WorkerW là child của Progman.
        /// </summary>
        public bool InitializeWorkerW()
        {
            Logger.Info("Initializing WorkerW hook...");

            // 1. Tìm Progman
            IntPtr progman = NativeMethods.FindWindow("Progman", null);
            if (progman == IntPtr.Zero)
            {
                Logger.Error($"Progman window not found! Win32Error: {Marshal.GetLastWin32Error()}");
                return false;
            }
            Logger.Info($"Found Progman: 0x{progman.ToInt64():X8}");

            // 2. Gửi message 0x052C để ép Windows tạo lớp WorkerW (nếu chưa có)
            IntPtr result;
            NativeMethods.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1),
                NativeMethods.SMTO_NORMAL, 1000, out result);
            NativeMethods.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.SMTO_NORMAL, 1000, out result);
            Logger.Info("Sent 0x052C messages to Progman.");

            // 3. Xác định vị trí SHELLDLL_DefView
            IntPtr shellDefViewParent = IntPtr.Zero;
            IntPtr defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (defView != IntPtr.Zero)
            {
                shellDefViewParent = progman;
                Logger.Info($"SHELLDLL_DefView (0x{defView.ToInt64():X8}) is inside Progman (0x{progman.ToInt64():X8})");
            }
            else
            {
                // Tìm trong các top-level WorkerW (Windows 10 / Windows 11 cũ)
                _enumWindowsCallback = (hWnd, lParam) =>
                {
                    StringBuilder sb = new StringBuilder(256);
                    NativeMethods.GetClassName(hWnd, sb, 256);
                    if (sb.ToString() == "WorkerW")
                    {
                        IntPtr childDef = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (childDef != IntPtr.Zero)
                        {
                            shellDefViewParent = hWnd;
                            Logger.Info($"SHELLDLL_DefView (0x{childDef.ToInt64():X8}) is inside WorkerW (0x{hWnd.ToInt64():X8})");
                            return false;
                        }
                    }
                    return true;
                };
                NativeMethods.EnumWindows(_enumWindowsCallback, IntPtr.Zero);
            }

            if (shellDefViewParent == IntPtr.Zero)
            {
                Logger.Error("Could not locate parent of SHELLDLL_DefView!");
                return false;
            }

            // 4. Tìm WorkerW mục tiêu
            IntPtr targetWorkerW = IntPtr.Zero;

            if (shellDefViewParent == progman)
            {
                // Windows 11 24H2: SHELLDLL_DefView trong Progman
                // Thử chiến lược 1: WorkerW con trực tiếp của Progman
                targetWorkerW = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
                if (targetWorkerW != IntPtr.Zero)
                {
                    Logger.Info($"[Win11-24H2] Found child WorkerW inside Progman: 0x{targetWorkerW.ToInt64():X8}");
                }
                else
                {
                    // Chiến lược 2: WorkerW top-level ngay sau Progman trong Z-order
                    targetWorkerW = NativeMethods.FindWindowEx(IntPtr.Zero, progman, "WorkerW", null);
                    if (targetWorkerW != IntPtr.Zero)
                    {
                        Logger.Info($"[Win11-24H2] Found WorkerW behind Progman in Z-order: 0x{targetWorkerW.ToInt64():X8}");
                    }
                }
            }
            else
            {
                // Windows 10 / Windows 11 cũ: WorkerW ở top-level, ngay sau shellDefViewParent
                targetWorkerW = NativeMethods.FindWindowEx(IntPtr.Zero, shellDefViewParent, "WorkerW", null);
                if (targetWorkerW != IntPtr.Zero)
                {
                    Logger.Info($"[Win10/11] Found WorkerW directly behind DefViewParent: 0x{targetWorkerW.ToInt64():X8}");
                }
            }

            // Chiến lược fallback cuối: Duyệt tất cả top-level windows, lấy WorkerW đầu tiên không có SHELLDLL_DefView
            if (targetWorkerW == IntPtr.Zero)
            {
                Logger.Warn("All primary strategies failed — scanning all WorkerW windows as fallback...");
                IntPtr defViewWorkerW = shellDefViewParent != progman ? shellDefViewParent : IntPtr.Zero;

                _enumWindowsCallback = (hWnd, lParam) =>
                {
                    StringBuilder sb = new StringBuilder(256);
                    NativeMethods.GetClassName(hWnd, sb, 256);
                    if (sb.ToString() == "WorkerW" && hWnd != defViewWorkerW)
                    {
                        IntPtr child = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (child == IntPtr.Zero)
                        {
                            targetWorkerW = hWnd;
                            Logger.Info($"[Fallback] Found WorkerW without DefView: 0x{hWnd.ToInt64():X8}");
                            return false;
                        }
                    }
                    return true;
                };
                NativeMethods.EnumWindows(_enumWindowsCallback, IntPtr.Zero);
            }

            if (targetWorkerW == IntPtr.Zero)
            {
                Logger.Error("Failed to find any valid wallpaper WorkerW window!");
                return false;
            }

            // Đảm bảo WorkerW mục tiêu hiển thị
            NativeMethods.ShowWindow(targetWorkerW, NativeMethods.SW_SHOW);
            bool isVisible = NativeMethods.IsWindowVisible(targetWorkerW);
            Logger.Info($"Determined target WorkerW: 0x{targetWorkerW.ToInt64():X8} | IsVisible: {isVisible}");

            _workerWHwnd = targetWorkerW;
            return true;
        }

        /// <summary>
        /// Gắn cửa sổ con vào WorkerW và định vị kích thước
        /// </summary>
        public bool AttachWindow(IntPtr childHwnd, Rectangle bounds)
        {
            if (_workerWHwnd == IntPtr.Zero)
            {
                Logger.Error("Cannot attach: WorkerW has not been initialized!");
                return false;
            }

            Logger.Info($"Attaching window 0x{childHwnd.ToInt64():X8} to WorkerW 0x{_workerWHwnd.ToInt64():X8} at {bounds}...");

            // Đổi window style thành WS_CHILD và gỡ WS_POPUP
            int style = NativeMethods.GetWindowLong(childHwnd, NativeMethods.GWL_STYLE);
            style = (style | NativeMethods.WS_CHILD) & ~NativeMethods.WS_POPUP;
            NativeMethods.SetWindowLong(childHwnd, NativeMethods.GWL_STYLE, style);

            IntPtr prevParent = NativeMethods.SetParent(childHwnd, _workerWHwnd);
            int setParentErr = Marshal.GetLastWin32Error();
            Logger.Info($"SetParent returned previous parent: 0x{prevParent.ToInt64():X8}, Win32Error: {setParentErr}");

            _attachedHwnd = childHwnd;

            // Định vị phủ kín màn hình và ép hiển thị
            bool posResult = NativeMethods.SetWindowPos(
                childHwnd,
                NativeMethods.HWND_BOTTOM,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            NativeMethods.ShowWindow(childHwnd, NativeMethods.SW_SHOW);
            NativeMethods.UpdateWindow(childHwnd);

            int posErr = Marshal.GetLastWin32Error();
            bool visible = NativeMethods.IsWindowVisible(childHwnd);
            Logger.Info($"SetWindowPos result: {posResult}, Win32Error: {posErr}, IsChildVisible: {visible}");

            return posResult;
        }

        /// <summary>
        /// Dọn dẹp và khôi phục desktop để không bị màn hình đen
        /// </summary>
        public void RestoreDesktop()
        {
            Logger.Info("Restoring desktop wallpaper and cleaning up hooks...");

            if (_attachedHwnd != IntPtr.Zero)
            {
                try { NativeMethods.SetParent(_attachedHwnd, IntPtr.Zero); }
                catch (Exception ex) { Logger.Error("Error resetting parent of attached window", ex); }
                _attachedHwnd = IntPtr.Zero;
            }

            bool spiResult = NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETDESKWALLPAPER, 0, null,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
            Logger.Info($"SystemParametersInfo(SPI_SETDESKWALLPAPER) returned: {spiResult}, Win32Error: {Marshal.GetLastWin32Error()}");
        }
    }
}
