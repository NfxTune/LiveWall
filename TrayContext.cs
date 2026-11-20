using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public class TrayContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private readonly DesktopManager _desktopManager;
        private readonly WallpaperForm _wallpaperForm;
        private readonly LiveWall.UI.MainForm _managerForm;
        private readonly LiveWall.UI.AppSettings _uiSettings;
        private readonly NotifyIcon _trayIcon;
        private bool _isExiting;

        public TrayContext(DesktopManager desktopManager, AppSettings settings)
        {
            _desktopManager = desktopManager;
            _settings = settings;

            // 1. Khởi tạo WallpaperForm ngầm trên desktop
            _wallpaperForm = new WallpaperForm(_desktopManager, _settings);
            _wallpaperForm.Show();

            // 2. Khởi tạo giao diện mới và ánh xạ cấu hình sang engine hiện có.
            _uiSettings = new LiveWall.UI.AppSettings
            {
                Library = new System.Collections.Generic.List<string>(_settings.VideoLibrary),
                Current = _settings.CurrentVideoPath,
                Active = _settings.CurrentVideoPath,
                MonitorIndex = _settings.SelectedMonitorIndex,
                Mute = _settings.IsMuted,
                Loop = _settings.LoopInf,
                Volume = _settings.Volume,
                PauseFullscreen = _settings.PauseOnFullscreen,
                PauseLock = _settings.PauseOnLock,
                PauseBattery = _settings.PauseOnBattery,
                AutoStart = AutoStartManager.IsEnabled()
            };
            _uiSettings.Persist = SyncUiSettingsToCore;
            _managerForm = new LiveWall.UI.MainForm(_uiSettings) { HideOnClose = true };
            _ = _managerForm.Handle; // Keep a discoverable window handle while the form is hidden in the tray.
            _managerForm.ApplyRequested += ApplyWallpaper;
            _managerForm.SettingsChanged += ApplyUiRuntimeSettings;
            _managerForm.PlaybackToggleRequested += TogglePause;
            _managerForm.ExitRequested += ExitApp;

            // 3. Khởi tạo System Tray Icon
            _trayIcon = new NotifyIcon
            {
                Icon = CreateAppIcon(),
                Text = "Live Wallpaper Studio",
                Visible = true
            };

            // Tạo Context Menu cho Tray Icon
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem itemOpen = new ToolStripMenuItem("🎬 Mở Bảng Quản Lý...", null, (s, e) => ShowManagerForm())
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            ToolStripMenuItem itemPause = new ToolStripMenuItem("⏸️ Tạm dừng / Tiếp tục", null, (s, e) => TogglePause());
            ToolStripMenuItem itemMute = new ToolStripMenuItem("🔇 Tắt / Bật tiếng", null, (s, e) => ToggleMute());
            ToolStripMenuItem itemAutoStart = new ToolStripMenuItem("⚙️ Khởi động cùng Windows", null, (s, e) => ToggleAutoStart());

            ToolStripMenuItem itemExit = new ToolStripMenuItem("❌ Thoát ứng dụng", null, (s, e) => ExitApp());

            menu.Items.Add(itemOpen);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemPause);
            menu.Items.Add(itemMute);
            menu.Items.Add(itemAutoStart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemExit);

            menu.Opening += (s, e) =>
            {
                bool isPaused = _wallpaperForm.Player?.IsPaused ?? false;
                itemPause.Text = isPaused ? "▶️ Tiếp tục phát" : "⏸️ Tạm dừng";
                itemMute.Text = _settings.IsMuted ? "🔊 Bật âm thanh" : "🔇 Tắt tiếng";
                itemAutoStart.Checked = AutoStartManager.IsEnabled();
            };

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => ShowManagerForm();

            Logger.Info("TrayContext initialized successfully.");

            // Luôn mở bảng điều khiển khi khởi động để người dùng có thể đổi hình nền/cấu hình.
            if (!Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase)))
                ShowManagerForm();
        }

        public void ShowManagerForm()
        {
            _managerForm.Show();
            _managerForm.WindowState = FormWindowState.Normal;
            _managerForm.Activate();
            _managerForm.SetPlaybackState(!(_wallpaperForm.Player?.IsPaused ?? false));
        }

        private void SyncUiSettingsToCore(LiveWall.UI.AppSettings ui)
        {
            _settings.VideoLibrary = new System.Collections.Generic.List<string>(ui.Library);
            _settings.CurrentVideoPath = ui.Active;
            _settings.SelectedMonitorIndex = ui.MonitorIndex;
            _settings.IsMuted = ui.Mute;
            _settings.LoopInf = ui.Loop;
            _settings.Volume = ui.Volume;
            _settings.PauseOnFullscreen = ui.PauseFullscreen;
            _settings.PauseOnLock = ui.PauseLock;
            _settings.PauseOnBattery = ui.PauseBattery;
            _settings.AutoStart = ui.AutoStart;
            _settings.Save();
        }

        private void ApplyUiRuntimeSettings(LiveWall.UI.AppSettings ui)
        {
            _wallpaperForm.SetMute(ui.Mute);
            _wallpaperForm.SetLoop(ui.Loop);
            _wallpaperForm.SetVolume(ui.Volume);
            if (_wallpaperForm.PauseManager is { } pauseManager)
            {
                pauseManager.PauseOnFullscreen = ui.PauseFullscreen;
                pauseManager.PauseOnLock = ui.PauseLock;
                pauseManager.PauseOnBattery = ui.PauseBattery;
            }
        }

        private void ApplyWallpaper(string path, int monitorIndex)
        {
            _uiSettings.Active = path;
            _uiSettings.MonitorIndex = monitorIndex;
            SyncUiSettingsToCore(_uiSettings);
            _wallpaperForm.SetMonitor(monitorIndex);
            _wallpaperForm.ChangeSource(path);
            _managerForm.SetPlaybackState(true);
        }

        private void TogglePause()
        {
            if (_wallpaperForm.Player == null) return;
            if (_wallpaperForm.Player.IsPaused)
            {
                _wallpaperForm.Player.Resume();
            }
            else
            {
                _wallpaperForm.Player.Pause();
            }
            _managerForm.SetPlaybackState(!_wallpaperForm.Player.IsPaused);
        }

        private void ToggleMute()
        {
            bool newMute = !_settings.IsMuted;
            _wallpaperForm.SetMute(newMute);
            _managerForm.SetPlaybackState(!(_wallpaperForm.Player?.IsPaused ?? false));
        }

        private void ToggleAutoStart()
        {
            bool current = AutoStartManager.IsEnabled();
            bool next = !current;
            AutoStartManager.SetAutoStart(next);
            _settings.AutoStart = next;
            _uiSettings.AutoStart = next;
            _settings.Save();
            _managerForm.SetAutoStartState(next);
            _managerForm.SetPlaybackState(!(_wallpaperForm.Player?.IsPaused ?? false));
        }

        public void ExitApp()
        {
            if (_isExiting) return;
            _isExiting = true;
            Logger.Info("ExitApp requested. Closing forms and cleaning up...");

            try
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            catch (Exception ex) { Logger.Error("Failed to dispose tray icon", ex); }

            try
            {
                _managerForm.Hide();
                _managerForm.Dispose();
            }
            catch (Exception ex) { Logger.Error("Failed to close manager window", ex); }

            try
            {
                if (!_wallpaperForm.IsDisposed) _wallpaperForm.Close();
            }
            catch (Exception ex) { Logger.Error("Failed to close wallpaper renderer", ex); }
            finally
            {
                try { _desktopManager.RestoreDesktop(); }
                catch (Exception ex) { Logger.Error("Failed to restore desktop during exit", ex); }
                ExitThread();
            }
        }

        private static Icon CreateAppIcon()
        {
            using (Icon? executableIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
            {
                if (executableIcon != null) return (Icon)executableIcon.Clone();
            }

            using Bitmap bmp = new Bitmap(32, 32);
            using Graphics g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Nền tròn tím gradient
            using LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, 32, 32),
                Color.FromArgb(203, 166, 247),
                Color.FromArgb(137, 180, 250),
                LinearGradientMode.ForwardDiagonal);
            g.FillEllipse(brush, 1, 1, 30, 30);

            // Biểu tượng nút Play trắng
            Point[] triangle = new Point[]
            {
                new Point(12, 9),
                new Point(23, 16),
                new Point(12, 23)
            };
            g.FillPolygon(Brushes.White, triangle);

            IntPtr hIcon = bmp.GetHicon();
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
    }
}
