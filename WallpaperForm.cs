using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public class WallpaperForm : Form
    {
        private readonly DesktopManager _desktopManager;
        private readonly AppSettings _settings;
        private MpvPlayer? _player;
        private PauseManager? _pauseManager;
        private WebWallpaperManager? _webManager;
        private string? _currentSource;
        private readonly uint _taskbarCreatedMsg;

        public MpvPlayer? Player => _player;
        public PauseManager? PauseManager => _pauseManager;
        public WebWallpaperManager? WebManager => _webManager;
        public string? CurrentSource => _currentSource;
        public bool IsWebMode => IsWebTarget(_currentSource);

        public WallpaperForm(DesktopManager desktopManager, AppSettings settings)
        {
            _desktopManager = desktopManager;
            _settings = settings;
            _currentSource = settings.CurrentVideoPath;
            _webManager = new WebWallpaperManager();
            _taskbarCreatedMsg = NativeMethods.RegisterWindowMessage("TaskbarCreated");

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;

            Rectangle bounds = GetMonitorBounds(settings.SelectedMonitorIndex);
            Bounds = bounds;

            Logger.Info($"WallpaperForm created with bounds: {bounds}");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Logger.Info($"WallpaperForm OnLoad called, handle: 0x{Handle.ToInt64():X8}");

            // 1. Gắn Form vào WorkerW
            bool attached = _desktopManager.AttachWindow(Handle, Bounds);
            if (!attached)
            {
                Logger.Error("Failed to attach WallpaperForm to WorkerW!");
                return;
            }

            // 2. Khởi tạo mpv player
            try
            {
                _player = new MpvPlayer(Handle, initialMute: _settings.IsMuted, initialVolume: _settings.Volume);

                // 3. Khởi tạo PauseManager thông minh
                _pauseManager = new PauseManager(
                    _player,
                    pauseOnFullscreen: _settings.PauseOnFullscreen,
                    pauseOnLock: _settings.PauseOnLock,
                    pauseOnBattery: _settings.PauseOnBattery);
                Logger.Info("PauseManager attached to WallpaperForm successfully.");

                // 4. Nạp nguồn hình nền ban đầu (Video hoặc Web)
                if (!string.IsNullOrEmpty(_currentSource))
                {
                    ApplyWallpaperSource(_currentSource);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize wallpaper renderers", ex);
                MessageBox.Show($"Lỗi khởi tạo: {ex.Message}\nVui lòng kiểm tra file log: {Logger.LogFilePath}", "Lỗi Live Wallpaper", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ChangeSource(string newSource)
        {
            if (string.IsNullOrEmpty(newSource)) return;

            _currentSource = newSource;
            _settings.CurrentVideoPath = newSource;
            _settings.Save();

            ApplyWallpaperSource(newSource);
        }

        public void SetMonitor(int monitorIndex)
        {
            _settings.SelectedMonitorIndex = monitorIndex;
            Bounds = GetMonitorBounds(monitorIndex);
            if (IsHandleCreated && !IsWebMode)
                _desktopManager.AttachWindow(Handle, Bounds);
        }

        private static Rectangle GetMonitorBounds(int monitorIndex)
        {
            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0) return new Rectangle(0, 0, 1920, 1080);
            if (monitorIndex < 0)
            {
                Rectangle combined = screens[0].Bounds;
                for (int i = 1; i < screens.Length; i++) combined = Rectangle.Union(combined, screens[i].Bounds);
                return combined;
            }
            return screens[Math.Clamp(monitorIndex, 0, screens.Length - 1)].Bounds;
        }

        private async void ApplyWallpaperSource(string source)
        {
            if (IsWebTarget(source))
            {
                Logger.Info($"Switching to Web Wallpaper Mode: '{source}'");
                // Tạm dừng video player
                _player?.Pause();
                // Khởi động Web Host process
                if (_webManager != null)
                {
                    await _webManager.StartAsync(source, _settings.SelectedMonitorIndex);
                }
            }
            else
            {
                Logger.Info($"Switching to Video Wallpaper Mode: '{source}'");
                // Dừng web host process nếu đang chạy
                _webManager?.Stop();

                // Phát video
                if (File.Exists(source))
                {
                    _player?.Play(source);
                }
            }
        }

        public static bool IsWebTarget(string? target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            return target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   target.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                   target.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
        }

        public void SetMute(bool mute)
        {
            _settings.IsMuted = mute;
            _settings.Save();
            _player?.SetMute(mute);
        }

        public void SetLoop(bool loopInf)
        {
            _settings.LoopInf = loopInf;
            _settings.Save();
            _player?.SetLoop(loopInf);
        }

        public void SetVolume(int volume)
        {
            _settings.Volume = volume;
            _settings.Save();
            _player?.SetVolume(volume);
        }

        protected override void WndProc(ref Message m)
        {
            if (_taskbarCreatedMsg != 0 && m.Msg == (int)_taskbarCreatedMsg)
            {
                Logger.Info("[Self-Healing] Received TaskbarCreated broadcast (Explorer restarted). Re-attaching WallpaperForm...");
                try
                {
                    if (_desktopManager.InitializeWorkerW())
                    {
                        _desktopManager.AttachWindow(Handle, Bounds);
                        NativeMethods.ShowWindow(Handle, NativeMethods.SW_SHOW);
                        NativeMethods.UpdateWindow(Handle);
                        Logger.Info("[Self-Healing] Re-attached successfully after Explorer restart.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to re-attach wallpaper after Explorer restart", ex);
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Logger.Info("WallpaperForm is closing. Performing cleanup...");

            _webManager?.Dispose();
            _webManager = null;

            _pauseManager?.Dispose();
            _pauseManager = null;

            _player?.Dispose();
            _player = null;

            _desktopManager.RestoreDesktop();

            base.OnFormClosing(e);
        }
    }
}
