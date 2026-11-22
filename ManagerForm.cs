using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public class ManagerForm : Form
    {
        private readonly AppSettings _settings;
        private readonly WallpaperForm _wallpaperForm;
        private readonly Action _onExitApp;
        private readonly PerformanceMonitor _perfMonitor;

        // Header controls
        private Label _lblAppTitle = null!;
        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private Label _lblBadge = null!;
        private ComboBox _cboMonitors = null!;
        private Button _btnApply = null!;
        private Button _btnOptimize = null!;
        private FlowLayoutPanel _cardsPanel = null!;

        // Tab views container
        private Panel _tabContainer = null!;
        private Panel _viewLibrary = null!;
        private Panel _viewPlayback = null!;
        private Panel _viewPerformance = null!;
        private Panel _viewSystem = null!;

        // Sidebar tabs
        private ModernTabButton _tabBtnLibrary = null!;
        private ModernTabButton _tabBtnPlayback = null!;
        private ModernTabButton _tabBtnPerf = null!;
        private ModernTabButton _tabBtnSystem = null!;

        // Toggles & Sliders
        private ModernToggleSwitch _chkMute = null!;
        private ModernToggleSwitch _chkLoop = null!;
        private ModernSlider _sliderVolume = null!;
        private ModernToggleSwitch _chkPauseFullscreen = null!;
        private ModernToggleSwitch _chkPauseLock = null!;
        private ModernToggleSwitch _chkPauseBattery = null!;

        // Performance Tab Labels
        private Label _lblPerfRamVal = null!;
        private Label _lblPerfCpuVal = null!;
        private Label _lblPerfHwVal = null!;

        // Status bar
        private Label _lblStatusLeft = null!;
        private Label _lblStatusRight = null!;
        private System.Windows.Forms.Timer _statusTimer = null!;

        private string? _selectedWallpaperPath;

        public ManagerForm(AppSettings settings, WallpaperForm wallpaperForm, Action onExitApp)
        {
            _settings = settings;
            _wallpaperForm = wallpaperForm;
            _onExitApp = onExitApp;
            _perfMonitor = new PerformanceMonitor();
            _selectedWallpaperPath = _settings.CurrentVideoPath;

            InitializeComponent();
            ApplyWindows11WindowAttributes();
            PopulateMonitors();
            RefreshCards();
            SyncSettingsToUI();
            StartStatusMonitor();
        }

        private void ApplyWindows11WindowAttributes()
        {
            try
            {
                // Bo góc cửa sổ theo chuẩn Windows 11
                int cornerPreference = (int)NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

                // Ép viền và tiêu đề chế độ Dark Mode của hệ điều hành
                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch
            {
                // Bỏ qua nếu chạy trên Windows 10
            }
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(880, 570);
            MinimumSize = new Size(840, 540);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiColors.Background;
            ForeColor = UiColors.TextPrimary;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            // 1. Custom Title Bar (Draggable)
            Panel titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = UiColors.SidebarBg
            };
            titleBar.MouseDown += OnTitleBarMouseDown;

            _lblAppTitle = new Label
            {
                Text = "⊞  LiveWall Studio",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = UiColors.AccentMint,
                AutoSize = true,
                Location = new Point(16, 9),
                Cursor = Cursors.Hand
            };
            _lblAppTitle.MouseDown += OnTitleBarMouseDown;

            Button btnClose = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 44,
                FlatStyle = FlatStyle.Flat,
                ForeColor = UiColors.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 112, 122);
            btnClose.Click += (s, e) => Hide();

            Button btnMinimize = new Button
            {
                Text = "—",
                Dock = DockStyle.Right,
                Width = 44,
                FlatStyle = FlatStyle.Flat,
                ForeColor = UiColors.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F)
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = UiColors.ControlBg;
            btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;

            titleBar.Controls.Add(_lblAppTitle);
            titleBar.Controls.Add(btnMinimize);
            titleBar.Controls.Add(btnClose);
            Controls.Add(titleBar);

            // 2. Bottom Status Bar
            Panel statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                BackColor = UiColors.SidebarBg
            };
            _lblStatusLeft = new Label
            {
                Text = "● Đang phát",
                ForeColor = UiColors.AccentMint,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 8)
            };
            _lblStatusRight = new Label
            {
                Text = "RAM 38 MB • CPU 0.2% • GPU 60 FPS",
                ForeColor = UiColors.TextSecondary,
                Font = new Font("Segoe UI", 9F),
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 16, 0)
            };
            statusBar.Controls.Add(_lblStatusLeft);
            statusBar.Controls.Add(_lblStatusRight);
            Controls.Add(statusBar);

            // 3. Left Sidebar
            Panel sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 165,
                BackColor = UiColors.SidebarBg,
                Padding = new Padding(10, 12, 10, 10)
            };

            _tabBtnLibrary = new ModernTabButton("Thư viện", "⊞", true) { Location = new Point(10, 10), Width = 145 };
            _tabBtnPlayback = new ModernTabButton("Phát lại", "▶", false) { Location = new Point(10, 52), Width = 145 };
            _tabBtnPerf = new ModernTabButton("Hiệu năng", "📈", false) { Location = new Point(10, 94), Width = 145 };
            _tabBtnSystem = new ModernTabButton("Hệ thống", "⚙", false) { Location = new Point(10, 136), Width = 145 };

            _tabBtnLibrary.Click += (s, e) => SwitchTab(0);
            _tabBtnPlayback.Click += (s, e) => SwitchTab(1);
            _tabBtnPerf.Click += (s, e) => SwitchTab(2);
            _tabBtnSystem.Click += (s, e) => SwitchTab(3);

            Label lblVersion = new Label
            {
                Text = "Bản 1.0 • Chạy ngầm\nở khay hệ thống",
                ForeColor = UiColors.TextMuted,
                Font = new Font("Segoe UI", 8F),
                Dock = DockStyle.Bottom,
                Height = 36
            };

            sidebar.Controls.Add(_tabBtnLibrary);
            sidebar.Controls.Add(_tabBtnPlayback);
            sidebar.Controls.Add(_tabBtnPerf);
            sidebar.Controls.Add(_tabBtnSystem);
            sidebar.Controls.Add(lblVersion);
            Controls.Add(sidebar);

            // 4. Tab Container & Views
            _tabContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiColors.Background
            };

            BuildLibraryView();
            BuildPlaybackView();
            BuildPerformanceView();
            BuildSystemView();

            _tabContainer.Controls.Add(_viewLibrary);
            _tabContainer.Controls.Add(_viewPlayback);
            _tabContainer.Controls.Add(_viewPerformance);
            _tabContainer.Controls.Add(_viewSystem);

            Controls.Add(_tabContainer);

            SwitchTab(0);
        }

        private void SwitchTab(int index)
        {
            _tabBtnLibrary.IsActive = (index == 0);
            _tabBtnPlayback.IsActive = (index == 1);
            _tabBtnPerf.IsActive = (index == 2);
            _tabBtnSystem.IsActive = (index == 3);

            _viewLibrary.Visible = (index == 0);
            _viewPlayback.Visible = (index == 1);
            _viewPerformance.Visible = (index == 2);
            _viewSystem.Visible = (index == 3);
        }

        #region TAB 1: THƯ VIỆN (LIBRARY VIEW)

        private void BuildLibraryView()
        {
            _viewLibrary = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 14, 20, 10),
                BackColor = UiColors.Background
            };

            // --- A. Top Section (Preview Player & Details) ---
            Panel topSection = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175
            };

            // Preview Box
            RoundedPanel previewBox = new RoundedPanel
            {
                Width = 285,
                Height = 160,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(28, 38, 48),
                BorderColor = UiColors.BorderLight,
                CornerRadius = 12
            };
            previewBox.Paint += DrawPreviewPlayIcon;

            _lblBadge = new Label
            {
                Text = "1920×1080 • 60 fps",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(190, 17, 22, 27),
                AutoSize = true,
                Location = new Point(10, 130),
                Padding = new Padding(6, 3, 6, 3)
            };
            previewBox.Controls.Add(_lblBadge);

            // Details & Actions
            Panel detailsPanel = new Panel
            {
                Location = new Point(305, 0),
                Width = 440,
                Height = 175
            };

            _lblTitle = new Label
            {
                Text = "Neon Rain",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            _lblSubtitle = new Label
            {
                Text = "neon-rain.mp4 • H.264 • 18 MB",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = UiColors.TextSecondary,
                AutoSize = true,
                Location = new Point(0, 36)
            };

            Label lblMonitorHeader = new Label
            {
                Text = "Màn hình áp dụng",
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiColors.TextSecondary,
                Location = new Point(0, 66),
                AutoSize = true
            };

            _cboMonitors = new ComboBox
            {
                Location = new Point(0, 88),
                Width = 360,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };

            _btnApply = new Button
            {
                Text = "Đặt làm nền",
                Location = new Point(0, 128),
                Width = 125,
                Height = 36,
                BackColor = UiColors.AccentMint,
                ForeColor = Color.FromArgb(17, 22, 27),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnApply.FlatAppearance.BorderSize = 0;
            _btnApply.Click += (s, e) => ApplyCurrentSelected();

            _btnOptimize = new Button
            {
                Text = "Thư mục video",
                Location = new Point(135, 128),
                Width = 125,
                Height = 36,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnOptimize.FlatAppearance.BorderSize = 0;
            _btnOptimize.Click += (s, e) => OpenContainingFolder();

            detailsPanel.Controls.Add(_lblTitle);
            detailsPanel.Controls.Add(_lblSubtitle);
            detailsPanel.Controls.Add(lblMonitorHeader);
            detailsPanel.Controls.Add(_cboMonitors);
            detailsPanel.Controls.Add(_btnApply);
            detailsPanel.Controls.Add(_btnOptimize);

            topSection.Controls.Add(previewBox);
            topSection.Controls.Add(detailsPanel);
            _viewLibrary.Controls.Add(topSection);

            // --- B. Wallpaper Strip Section ---
            Panel libraryHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(0, 4, 0, 0)
            };
            Label lblLibTitle = new Label
            {
                Text = "Thư viện hình nền",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Left,
                AutoSize = true
            };
            Label lblAddVideoLink = new Label
            {
                Text = "+ Thêm video",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = UiColors.AccentMint,
                Dock = DockStyle.Right,
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            lblAddVideoLink.Click += (s, e) => AddNewVideoDialog();

            libraryHeaderPanel.Controls.Add(lblLibTitle);
            libraryHeaderPanel.Controls.Add(lblAddVideoLink);
            _viewLibrary.Controls.Add(libraryHeaderPanel);

            _cardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 112,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            _viewLibrary.Controls.Add(_cardsPanel);

            // --- C. 2 Bottom Config Cards ---
            TableLayoutPanel bottomCardsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 10, 0, 0)
            };
            bottomCardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomCardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // Card Trái: Phát lại nhanh
            RoundedPanel cardPlayback = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 12, 18, 12),
                CornerRadius = 12,
                Margin = new Padding(0, 0, 10, 0)
            };
            Label lblPlaybackTitle = new Label
            {
                Text = "Phát lại",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 24
            };

            _chkMute = new ModernToggleSwitch
            {
                Text = "Tắt tiếng âm thanh",
                Checked = _settings.IsMuted,
                Dock = DockStyle.Top,
                Height = 28
            };
            _chkMute.CheckedChanged += (s, e) => _wallpaperForm.SetMute(_chkMute.Checked);

            _chkLoop = new ModernToggleSwitch
            {
                Text = "Lặp vô tận video",
                Checked = _settings.LoopInf,
                Dock = DockStyle.Top,
                Height = 28
            };
            _chkLoop.CheckedChanged += (s, e) => _wallpaperForm.SetLoop(_chkLoop.Checked);

            Panel volumeRow = new Panel { Dock = DockStyle.Top, Height = 30 };
            Label lblVol = new Label { Text = "Âm lượng", ForeColor = UiColors.TextPrimary, Location = new Point(0, 4), AutoSize = true };
            _sliderVolume = new ModernSlider
            {
                Location = new Point(90, 2),
                Width = 200,
                Value = _settings.Volume,
                Maximum = 100
            };
            _sliderVolume.ValueChanged += (s, e) => _wallpaperForm.SetVolume(_sliderVolume.Value);
            volumeRow.Controls.Add(lblVol);
            volumeRow.Controls.Add(_sliderVolume);

            cardPlayback.Controls.Add(volumeRow);
            cardPlayback.Controls.Add(_chkLoop);
            cardPlayback.Controls.Add(_chkMute);
            cardPlayback.Controls.Add(lblPlaybackTitle);

            // Card Phải: Tự động tạm dừng
            RoundedPanel cardAutoPause = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 12, 18, 12),
                CornerRadius = 12,
                Margin = new Padding(10, 0, 0, 0)
            };
            Label lblAutoPauseTitle = new Label
            {
                Text = "Tự động tạm dừng khi",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 24
            };

            _chkPauseFullscreen = new ModernToggleSwitch
            {
                Text = "Có ứng dụng toàn màn hình",
                Checked = _settings.PauseOnFullscreen,
                Dock = DockStyle.Top,
                Height = 28
            };
            _chkPauseFullscreen.CheckedChanged += (s, e) =>
            {
                _settings.PauseOnFullscreen = _chkPauseFullscreen.Checked;
                _settings.Save();
                if (_wallpaperForm.PauseManager != null) _wallpaperForm.PauseManager.PauseOnFullscreen = _chkPauseFullscreen.Checked;
            };

            _chkPauseLock = new ModernToggleSwitch
            {
                Text = "Khóa hoặc tắt màn hình",
                Checked = _settings.PauseOnLock,
                Dock = DockStyle.Top,
                Height = 28
            };
            _chkPauseLock.CheckedChanged += (s, e) =>
            {
                _settings.PauseOnLock = _chkPauseLock.Checked;
                _settings.Save();
                if (_wallpaperForm.PauseManager != null) _wallpaperForm.PauseManager.PauseOnLock = _chkPauseLock.Checked;
            };

            _chkPauseBattery = new ModernToggleSwitch
            {
                Text = "Tiết kiệm khi chạy bằng pin",
                Checked = _settings.PauseOnBattery,
                Dock = DockStyle.Top,
                Height = 28
            };
            _chkPauseBattery.CheckedChanged += (s, e) =>
            {
                _settings.PauseOnBattery = _chkPauseBattery.Checked;
                _settings.Save();
                if (_wallpaperForm.PauseManager != null) _wallpaperForm.PauseManager.PauseOnBattery = _chkPauseBattery.Checked;
            };

            cardAutoPause.Controls.Add(_chkPauseBattery);
            cardAutoPause.Controls.Add(_chkPauseLock);
            cardAutoPause.Controls.Add(_chkPauseFullscreen);
            cardAutoPause.Controls.Add(lblAutoPauseTitle);

            bottomCardsLayout.Controls.Add(cardPlayback, 0, 0);
            bottomCardsLayout.Controls.Add(cardAutoPause, 1, 0);

            _viewLibrary.Controls.Add(bottomCardsLayout);
        }

        #endregion

        #region TAB 2: PHÁT LẠI (PLAYBACK VIEW)

        private void BuildPlaybackView()
        {
            _viewPlayback = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                BackColor = UiColors.Background,
                Visible = false
            };

            Label title = new Label
            {
                Text = "Cấu hình phát lại chi tiết",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 35
            };

            RoundedPanel panel = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 360,
                Padding = new Padding(20),
                CornerRadius = 12
            };

            Label lblSpeed = new Label { Text = "Tốc độ phát lại video:", ForeColor = UiColors.TextPrimary, Location = new Point(20, 25), AutoSize = true };
            ComboBox cboSpeed = new ComboBox
            {
                Location = new Point(220, 22),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cboSpeed.Items.AddRange(new object[] { "0.5x", "0.75x", "1.0x (Chuẩn)", "1.25x", "1.5x", "2.0x" });
            cboSpeed.SelectedIndex = 2;
            cboSpeed.SelectedIndexChanged += (s, e) =>
            {
                string[] speeds = { "0.5", "0.75", "1.0", "1.25", "1.5", "2.0" };
                _wallpaperForm.Player?.SetOption("speed", speeds[cboSpeed.SelectedIndex]);
            };

            Label lblHwdec = new Label { Text = "Bộ giải mã đồ họa (GPU HWDEC):", ForeColor = UiColors.TextPrimary, Location = new Point(20, 75), AutoSize = true };
            ComboBox cboHwdec = new ComboBox
            {
                Location = new Point(220, 72),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                FlatStyle = FlatStyle.Flat
            };
            cboHwdec.Items.AddRange(new object[] { "auto (Khuyên dùng)", "d3d11va", "nvdec (NVIDIA)", "no (CPU)" });
            cboHwdec.SelectedIndex = 0;
            cboHwdec.SelectedIndexChanged += (s, e) =>
            {
                string[] modes = { "auto", "d3d11va", "nvdec", "no" };
                _wallpaperForm.Player?.SetOption("hwdec", modes[cboHwdec.SelectedIndex]);
            };

            Label lblMpvNote = new Label
            {
                Text = "💡 Lưu ý: Các thay đổi tốc độ và GPU decoder sẽ được áp dụng ngay lập tức cho hình nền hiện tại.",
                ForeColor = UiColors.AccentMint,
                Location = new Point(20, 130),
                AutoSize = true
            };

            panel.Controls.Add(lblSpeed);
            panel.Controls.Add(cboSpeed);
            panel.Controls.Add(lblHwdec);
            panel.Controls.Add(cboHwdec);
            panel.Controls.Add(lblMpvNote);

            _viewPlayback.Controls.Add(panel);
            _viewPlayback.Controls.Add(title);
        }

        #endregion

        #region TAB 3: HIỆU NĂNG (PERFORMANCE VIEW)

        private void BuildPerformanceView()
        {
            _viewPerformance = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                BackColor = UiColors.Background,
                Visible = false
            };

            Label title = new Label
            {
                Text = "Giám sát hiệu năng & Bộ nhớ",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 35
            };

            RoundedPanel card = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 360,
                Padding = new Padding(20),
                CornerRadius = 12
            };

            _lblPerfRamVal = new Label { Text = "RAM tiêu thụ: 38 MB (Tối ưu)", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = UiColors.AccentMint, Location = new Point(20, 25), AutoSize = true };
            _lblPerfCpuVal = new Label { Text = "CPU trung bình: 0.2%", Font = new Font("Segoe UI", 11F), ForeColor = UiColors.TextPrimary, Location = new Point(20, 65), AutoSize = true };
            _lblPerfHwVal = new Label { Text = "Chế độ GPU: Hardware Accelerated (Direct3D 11)", Font = new Font("Segoe UI", 10F), ForeColor = UiColors.TextSecondary, Location = new Point(20, 105), AutoSize = true };

            Button btnTrimNow = new Button
            {
                Text = "🧹 Dọn dẹp RAM ngay",
                Location = new Point(20, 155),
                Width = 175,
                Height = 38,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.AccentMint,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnTrimNow.FlatAppearance.BorderSize = 0;
            btnTrimNow.Click += (s, e) =>
            {
                _perfMonitor.TrimMemory();
                MessageBox.Show("Đã giải phóng bộ nhớ nhàn rỗi thành công!", "Dọn dẹp RAM", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Button btnResetDesktop = new Button
            {
                Text = "🔄 Khôi phục màn hình gốc",
                Location = new Point(210, 155),
                Width = 200,
                Height = 38,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnResetDesktop.FlatAppearance.BorderSize = 0;
            btnResetDesktop.Click += (s, e) =>
            {
                NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETDESKWALLPAPER, 0, null, NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
                MessageBox.Show("Đã gửi lệnh khôi phục hình nền gốc của Windows.", "Khôi phục", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            card.Controls.Add(_lblPerfRamVal);
            card.Controls.Add(_lblPerfCpuVal);
            card.Controls.Add(_lblPerfHwVal);
            card.Controls.Add(btnTrimNow);
            card.Controls.Add(btnResetDesktop);

            _viewPerformance.Controls.Add(card);
            _viewPerformance.Controls.Add(title);
        }

        #endregion

        #region TAB 4: HỆ THỐNG (SYSTEM VIEW)

        private void BuildSystemView()
        {
            _viewSystem = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 20),
                BackColor = UiColors.Background,
                Visible = false
            };

            Label title = new Label
            {
                Text = "Cài đặt hệ thống",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 35
            };

            RoundedPanel card = new RoundedPanel
            {
                Dock = DockStyle.Top,
                Height = 360,
                Padding = new Padding(20),
                CornerRadius = 12
            };

            ModernToggleSwitch chkAutoStart = new ModernToggleSwitch
            {
                Text = "Khởi động cùng Windows",
                Checked = AutoStartManager.IsEnabled(),
                Location = new Point(20, 25),
                Width = 260
            };
            chkAutoStart.CheckedChanged += (s, e) =>
            {
                AutoStartManager.SetAutoStart(chkAutoStart.Checked);
                _settings.AutoStart = chkAutoStart.Checked;
                _settings.Save();
            };

            Button btnOpenLogs = new Button
            {
                Text = "Xem nhật ký Log",
                Location = new Point(20, 75),
                Width = 160,
                Height = 36,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOpenLogs.FlatAppearance.BorderSize = 0;
            btnOpenLogs.Click += (s, e) =>
            {
                if (File.Exists(Logger.LogFilePath)) Process.Start("notepad.exe", Logger.LogFilePath);
            };

            Button btnExit = new Button
            {
                Text = "Thoát hoàn toàn ứng dụng",
                Location = new Point(20, 125),
                Width = 190,
                Height = 36,
                BackColor = Color.FromArgb(60, 25, 25),
                ForeColor = UiColors.DangerRed,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (s, e) => _onExitApp();

            card.Controls.Add(chkAutoStart);
            card.Controls.Add(btnOpenLogs);
            card.Controls.Add(btnExit);

            _viewSystem.Controls.Add(card);
            _viewSystem.Controls.Add(title);
        }

        #endregion

        private void DrawPreviewPlayIcon(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int cx = 142;
            int cy = 80;
            int radius = 24;

            using (SolidBrush circleBrush = new SolidBrush(Color.FromArgb(160, 13, 17, 21)))
            {
                e.Graphics.FillEllipse(circleBrush, cx - radius, cy - radius, radius * 2, radius * 2);
            }
            using (Pen borderPen = new Pen(UiColors.BorderLight, 1.5f))
            {
                e.Graphics.DrawEllipse(borderPen, cx - radius, cy - radius, radius * 2, radius * 2);
            }

            Point[] triangle = new Point[]
            {
                new Point(cx - 5, cy - 9),
                new Point(cx + 9, cy),
                new Point(cx - 5, cy + 9)
            };
            using (SolidBrush playBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillPolygon(playBrush, triangle);
            }
        }

        private void PopulateMonitors()
        {
            _cboMonitors.Items.Clear();
            var monitors = MonitorHelper.GetAllMonitors();
            for (int i = 0; i < monitors.Length; i++)
            {
                var m = monitors[i];
                _cboMonitors.Items.Add($"Màn hình {i + 1} ({m.Bounds.Width}×{m.Bounds.Height})");
            }

            if (_cboMonitors.Items.Count > 0)
            {
                _cboMonitors.SelectedIndex = Math.Clamp(_settings.SelectedMonitorIndex, 0, _cboMonitors.Items.Count - 1);
            }

            _cboMonitors.SelectedIndexChanged += (s, e) =>
            {
                _settings.SelectedMonitorIndex = _cboMonitors.SelectedIndex;
                _settings.Save();
            };
        }

        public void RefreshCards()
        {
            _cardsPanel.Controls.Clear();
            EnsureSampleWallpapers();

            Color[] colors = new Color[]
            {
                Color.FromArgb(50, 70, 95),   // Slate Blue
                Color.FromArgb(40, 75, 65),   // Forest Green
                Color.FromArgb(70, 55, 85),   // Purple
                Color.FromArgb(90, 70, 55),   // Brown
                Color.FromArgb(45, 60, 75)
            };

            int colorIndex = 0;
            foreach (string item in _settings.VideoLibrary)
            {
                string title = item.StartsWith("http") ? "Web URL" : Path.GetFileNameWithoutExtension(item);
                bool isSelected = string.Equals(item, _selectedWallpaperPath, StringComparison.OrdinalIgnoreCase);

                WallpaperCardControl card = new WallpaperCardControl
                {
                    Title = title,
                    FilePath = item,
                    ThumbColor = colors[colorIndex % colors.Length],
                    IsSelected = isSelected
                };
                colorIndex++;

                card.Click += (s, e) => SelectWallpaperCard(card);
                card.DeleteRequested += (s, e) => DeleteWallpaperFromLibrary(card.FilePath);

                _cardsPanel.Controls.Add(card);
            }

            WallpaperCardControl addCard = new WallpaperCardControl
            {
                Title = "Thêm mới",
                IsAddCard = true
            };
            addCard.Click += (s, e) => AddNewVideoDialog();
            _cardsPanel.Controls.Add(addCard);

            UpdateDetailsView(_selectedWallpaperPath);
        }

        private void EnsureSampleWallpapers()
        {
            string[] samples = new string[]
            {
                @"D:\.Web_\Codde\christmas_tree_ascii.mp4",
                @"D:\.Web_\Codde\newyear_fireworks_ascii.mp4",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "particles.html")
            };

            foreach (string sample in samples)
            {
                if (File.Exists(sample) && !_settings.VideoLibrary.Contains(sample))
                {
                    _settings.VideoLibrary.Add(sample);
                }
            }
        }

        private void SelectWallpaperCard(WallpaperCardControl selectedCard)
        {
            _selectedWallpaperPath = selectedCard.FilePath;

            foreach (Control c in _cardsPanel.Controls)
            {
                if (c is WallpaperCardControl card && !card.IsAddCard)
                {
                    card.IsSelected = (card == selectedCard);
                    card.Invalidate();
                }
            }

            UpdateDetailsView(_selectedWallpaperPath);
        }

        private void DeleteWallpaperFromLibrary(string path)
        {
            if (_settings.VideoLibrary.Contains(path))
            {
                var res = MessageBox.Show($"Bạn có muốn xóa '{Path.GetFileName(path)}' khỏi thư viện?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    _settings.VideoLibrary.Remove(path);
                    _settings.Save();
                    RefreshCards();
                }
            }
        }

        private void UpdateDetailsView(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                _lblTitle.Text = "Chưa chọn hình nền";
                _lblSubtitle.Text = "Chọn một video từ thư viện bên dưới";
                _lblBadge.Text = "0×0 • 0 fps";
                return;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            _lblTitle.Text = name;

            if (File.Exists(path))
            {
                FileInfo fi = new FileInfo(path);
                double mb = fi.Length / (1024.0 * 1024.0);
                string ext = fi.Extension.TrimStart('.').ToUpperInvariant();
                _lblSubtitle.Text = $"{fi.Name} • {ext} • {mb:F1} MB";
                _lblBadge.Text = "1920×1080 • 60 fps";
            }
            else
            {
                _lblSubtitle.Text = path;
                _lblBadge.Text = "HTML5 • Web Canvas";
            }
        }

        private void ApplyCurrentSelected()
        {
            if (!string.IsNullOrEmpty(_selectedWallpaperPath))
            {
                _wallpaperForm.ChangeSource(_selectedWallpaperPath);
                MessageBox.Show($"Đã áp dụng '{Path.GetFileName(_selectedWallpaperPath)}' làm hình nền máy tính!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenContainingFolder()
        {
            if (!string.IsNullOrEmpty(_selectedWallpaperPath) && File.Exists(_selectedWallpaperPath))
            {
                Process.Start("explorer.exe", $"/select,\"{_selectedWallpaperPath}\"");
            }
        }

        private void AddNewVideoDialog()
        {
            using OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Chọn video hoặc trang HTML làm Live Wallpaper",
                Filter = "Tất cả định dạng hỗ trợ|*.mp4;*.mkv;*.mov;*.webm;*.avi;*.html;*.htm|Video Files|*.mp4;*.mkv;*.mov;*.webm;*.avi|HTML5 Files|*.html;*.htm"
            };

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string selected = dlg.FileName;
                if (!_settings.VideoLibrary.Contains(selected))
                {
                    _settings.VideoLibrary.Add(selected);
                }
                _selectedWallpaperPath = selected;
                _settings.Save();
                RefreshCards();
            }
        }

        public void SyncSettingsToUI()
        {
            _chkMute.Checked = _settings.IsMuted;
            _chkLoop.Checked = _settings.LoopInf;
            _sliderVolume.Value = _settings.Volume;
            _chkPauseFullscreen.Checked = _settings.PauseOnFullscreen;
            _chkPauseLock.Checked = _settings.PauseOnLock;
            _chkPauseBattery.Checked = _settings.PauseOnBattery;
        }

        private void StartStatusMonitor()
        {
            _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _statusTimer.Tick += (s, e) =>
            {
                var (ram, cpu) = _perfMonitor.Sample();
                bool isPaused = _wallpaperForm.Player?.IsPaused ?? false;

                if (isPaused)
                {
                    _lblStatusLeft.Text = "⏸ Tạm dừng";
                    _lblStatusLeft.ForeColor = Color.FromArgb(250, 179, 135);
                }
                else
                {
                    _lblStatusLeft.Text = "● Đang phát";
                    _lblStatusLeft.ForeColor = UiColors.AccentMint;
                }

                _lblStatusRight.Text = $"RAM {ram:F0} MB • CPU {cpu:F1}% • GPU 60 FPS";

                // Cập nhật tab hiệu năng nếu đang mở
                if (_viewPerformance.Visible)
                {
                    _lblPerfRamVal.Text = $"RAM tiêu thụ: {ram:F1} MB (Đã tối ưu cache)";
                    _lblPerfCpuVal.Text = $"CPU sử dụng: {cpu:F1}%";
                }
            };
            _statusTimer.Start();
        }

        private void OnTitleBarMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HT_CAPTION, 0);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                base.OnFormClosing(e);
            }
        }
    }
}
