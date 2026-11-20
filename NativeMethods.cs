using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LiveWallpaper
{
    public static class NativeMethods
    {
        #region Win32 Constants

        public const uint SMTO_NORMAL = 0x0000;
        public const uint SMTO_BLOCK = 0x0001;
        public const uint SMTO_ABORTIFHUNG = 0x0002;

        public const int GWL_STYLE = -16;
        public const int WS_VISIBLE = 0x10000000;
        public const int WS_CHILD = 0x40000000;
        public const int WS_POPUP = unchecked((int)0x80000000);

        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOW = 5;

        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const uint SPI_SETDESKWALLPAPER = 0x0014;
        public const uint SPIF_UPDATEINIFILE = 0x01;
        public const uint SPIF_SENDCHANGE = 0x02;

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        public const uint MONITOR_DEFAULTTONEAREST = 2;

        #endregion

        #region Win32 Structs

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;

            public override string ToString() => $"[({Left}, {Top}) - ({Right}, {Bottom}) {Width}x{Height}]";
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        #endregion

        #region Win32 P/Invoke

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hWnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            string? pvParam,
            uint fWinIni);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        #endregion

        #region libmpv-2.dll P/Invoke

        private const string MpvDll = "libmpv-2.dll";

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_create")]
        public static extern IntPtr MpvCreate();

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_initialize")]
        public static extern int MpvInitialize(IntPtr ctx);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_option_string")]
        public static extern int MpvSetOptionString(
            IntPtr ctx,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command")]
        public static extern int MpvCommand(IntPtr ctx, [In] IntPtr[] args);

        public static int MpvCommandUtf8(IntPtr ctx, params string[] args)
        {
            if (ctx == IntPtr.Zero) return -1;

            // Mpv command nhận một mảng con trỏ chuỗi UTF-8 kết thúc bằng con trỏ NULL (IntPtr.Zero)
            IntPtr[] ptrs = new IntPtr[args.Length + 1];
            for (int i = 0; i < args.Length; i++)
            {
                ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
            }
            ptrs[args.Length] = IntPtr.Zero;

            try
            {
                return MpvCommand(ctx, ptrs);
            }
            finally
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (ptrs[i] != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(ptrs[i]);
                    }
                }
            }
        }

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_command_string")]
        public static extern int MpvCommandString(
            IntPtr ctx,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string args);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_set_property_string")]
        public static extern int MpvSetPropertyString(
            IntPtr ctx,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_error_string")]
        public static extern IntPtr MpvErrorString(int error);

        [DllImport(MpvDll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mpv_terminate_destroy")]
        public static extern void MpvTerminateDestroy(IntPtr ctx);

        public static string GetMpvError(int error)
        {
            IntPtr ptr = MpvErrorString(error);
            if (ptr == IntPtr.Zero) return $"Unknown error ({error})";
            return Marshal.PtrToStringUTF8(ptr) ?? $"Unknown error ({error})";
        }

        #endregion
    }
}
