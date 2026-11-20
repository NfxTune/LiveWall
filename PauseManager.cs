using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LiveWallpaper
{
    [Flags]
    public enum PauseReason
    {
        None = 0,
        FullscreenApp = 1 << 0,
        SessionLocked = 1 << 1,
        PowerSuspended = 1 << 2,
        OnBattery = 1 << 3,
        Manual = 1 << 4
    }

    public class PauseManager : IDisposable
    {
        // Static delegate reference to prevent GC garbage collection
        private static NativeMethods.WinEventDelegate? _staticWinEventProc;

        private readonly MpvPlayer _player;
        private IntPtr _hookHandle = IntPtr.Zero;
        private PauseReason _activeReasons = PauseReason.None;
        private bool _pauseOnFullscreen = true;
        private bool _pauseOnLock = true;
        private bool _pauseOnBattery = true;
        private bool _disposed = false;

        public PauseReason ActiveReasons => _activeReasons;
        public bool PauseOnFullscreen
        {
            get => _pauseOnFullscreen;
            set
            {
                _pauseOnFullscreen = value;
                if (value) EvaluateForegroundWindow(NativeMethods.GetForegroundWindow());
                else RemoveReason(PauseReason.FullscreenApp);
            }
        }

        public bool PauseOnLock
        {
            get => _pauseOnLock;
            set
            {
                _pauseOnLock = value;
                if (!value) RemoveReason(PauseReason.SessionLocked);
            }
        }

        public bool PauseOnBattery
        {
            get => _pauseOnBattery;
            set
            {
                _pauseOnBattery = value;
                EvaluateBatteryStatus();
            }
        }

        public PauseManager(MpvPlayer player, bool pauseOnFullscreen = true, bool pauseOnLock = true, bool pauseOnBattery = true)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _pauseOnFullscreen = pauseOnFullscreen;
            _pauseOnLock = pauseOnLock;
            _pauseOnBattery = pauseOnBattery;

            InitializeHooks();
        }

        private void InitializeHooks()
        {
            Logger.Info("Initializing smart event-driven PauseManager...");

            // 1. Hook EVENT_SYSTEM_FOREGROUND using SetWinEventHook (No polling, pure Win32 event push)
            _staticWinEventProc = OnForegroundChanged;
            _hookHandle = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _staticWinEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            int hookErr = Marshal.GetLastWin32Error();
            if (_hookHandle == IntPtr.Zero)
            {
                Logger.Error($"Failed to install SetWinEventHook! Win32Error: {hookErr}");
            }
            else
            {
                Logger.Info($"SetWinEventHook installed successfully, hook handle: 0x{_hookHandle.ToInt64():X8}");
            }

            // 2. Listen to SessionSwitch (Screen Lock / Unlock)
            SystemEvents.SessionSwitch += OnSessionSwitch;

            // 3. Listen to PowerModeChanged (Sleep / Resume)
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // 4. Initial evaluation
            EvaluateForegroundWindow(NativeMethods.GetForegroundWindow());
            EvaluateBatteryStatus();
        }

        private void OnForegroundChanged(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hWnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            if (hWnd == IntPtr.Zero) return;
            EvaluateForegroundWindow(hWnd);
        }

        private void EvaluateForegroundWindow(IntPtr hWnd)
        {
            if (!_pauseOnFullscreen)
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            if (hWnd == IntPtr.Zero)
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            // Ignore minimized windows
            if (NativeMethods.IsIconic(hWnd))
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            // Check Window Class name
            StringBuilder sb = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, sb, 256);
            string className = sb.ToString();

            // Shell & Desktop windows are NOT fullscreen applications
            if (className == "Progman" ||
                className == "WorkerW" ||
                className == "Shell_TrayWnd" ||
                className == "Shell_SecondaryTrayWnd" ||
                className == "Windows.UI.Core.CoreWindow")
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            // Get window rect
            if (!NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT winRect))
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            // Get monitor info
            IntPtr hMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            NativeMethods.MONITORINFO mi = new NativeMethods.MONITORINFO();
            mi.cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>();
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                RemoveReason(PauseReason.FullscreenApp);
                return;
            }

            // Window is considered fullscreen if it covers the entire monitor
            bool isFullscreen =
                winRect.Left <= mi.rcMonitor.Left &&
                winRect.Top <= mi.rcMonitor.Top &&
                winRect.Right >= mi.rcMonitor.Right &&
                winRect.Bottom >= mi.rcMonitor.Bottom;

            if (isFullscreen && NativeMethods.IsWindowVisible(hWnd))
            {
                StringBuilder titleSb = new StringBuilder(256);
                NativeMethods.GetWindowText(hWnd, titleSb, 256);
                string title = titleSb.ToString();

                Logger.Info($"[AutoPause] Fullscreen window detected: 0x{hWnd.ToInt64():X8} | Title: '{title}' | Class: '{className}' | Rect: {winRect}");
                AddReason(PauseReason.FullscreenApp);
            }
            else
            {
                RemoveReason(PauseReason.FullscreenApp);
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            Logger.Info($"[AutoPause] System SessionSwitch event: {e.Reason}");
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    if (_pauseOnLock) AddReason(PauseReason.SessionLocked);
                    break;
                case SessionSwitchReason.SessionUnlock:
                    RemoveReason(PauseReason.SessionLocked);
                    // Re-check foreground app when user logs back in
                    EvaluateForegroundWindow(NativeMethods.GetForegroundWindow());
                    break;
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            Logger.Info($"[AutoPause] PowerModeChanged event: {e.Mode}");
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    AddReason(PauseReason.PowerSuspended);
                    break;
                case PowerModes.Resume:
                    RemoveReason(PauseReason.PowerSuspended);
                    break;
                case PowerModes.StatusChange:
                    EvaluateBatteryStatus();
                    break;
            }
        }

        public void EvaluateBatteryStatus()
        {
            if (!_pauseOnBattery)
            {
                RemoveReason(PauseReason.OnBattery);
                return;
            }

            bool isOffline = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            if (isOffline)
            {
                Logger.Info("[AutoPause] Running on battery power. Pausing wallpaper to preserve battery.");
                AddReason(PauseReason.OnBattery);
            }
            else
            {
                RemoveReason(PauseReason.OnBattery);
            }
        }

        private void AddReason(PauseReason reason)
        {
            _activeReasons |= reason;
            ApplyState();
        }

        private void RemoveReason(PauseReason reason)
        {
            _activeReasons &= ~reason;
            ApplyState();
        }

        private void ApplyState()
        {
            if (_activeReasons != PauseReason.None)
            {
                if (!_player.IsPaused)
                {
                    Logger.Info($"Applying PAUSE command. Active reasons: {_activeReasons}");
                    _player.Pause();
                }
            }
            else
            {
                if (_player.IsPaused)
                {
                    Logger.Info("All pause reasons cleared. Applying RESUME command.");
                    _player.Resume();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Logger.Info("Disposing PauseManager and unhooking WinEvents...");

            if (_hookHandle != IntPtr.Zero)
            {
                bool unhookResult = NativeMethods.UnhookWinEvent(_hookHandle);
                Logger.Info($"UnhookWinEvent result: {unhookResult}");
                _hookHandle = IntPtr.Zero;
            }

            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            }
            catch (Exception ex)
            {
                Logger.Error("Error unsubscribing SystemEvents", ex);
            }

            _staticWinEventProc = null;
        }
    }
}
