using System;
using System.Collections.Generic;
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

        // Custom UI controls
        private Label _lblTitle = null!;
        private Label _lblSubtitle = null!;
        private Label _lblBadge = null!;
        private ComboBox _cboMonitors = null!;
        private Button _btnApply = null!;
        private Button _btnOptimize = null!;
        private FlowLayoutPanel _cardsPanel = null!;
        private ModernCheckBox _chkMute = null!;
        private ModernCheckBox _chkLoop = null!;
        private ModernSlider _sliderVolume = null!;
        private ModernCheckBox _chkPauseFullscreen = null!;
        private ModernCheckBox _chkPauseLock = null!;
        private ModernCheckBox _chkPauseBattery = null!;
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
            PopulateMonitors();
            RefreshCards();
            SyncSettingsToUI();
            StartStatusMonitor();
        }

        private void InitializeComponent()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(880, 560);
            MinimumSize = new Size(800, 540);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiColors.Background;
            ForeColor = UiColors.TextPrimary;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            // 1. Custom Title Bar (Draggable)
            Panel titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = UiColors.SidebarBg
            };
            titleBar.MouseDown += OnTitleBarMouseDown;

            // Window Icon & App Title
            Label lblAppTitle = new Label
            {
                Text = "▣  LiveWall",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = UiColors.AccentMint,
                AutoSize = true,
                Location = new Point(16, 9),
                Cursor = Cursors.Hand
            };
            lblAppTitle.MouseDown += OnTitleBarMouseDown;

            // Minimize & Close Buttons
            Button btnClose = new Button
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 42,
                FlatStyle = FlatStyle.Flat,
                ForeColor = UiColors.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 112, 122);
            btnClose.Click += (s, e) => Hide(); // Ẩn xuống khay hệ thống

            Button btnMinimize = new Button
            {
                Text = "—",
                Dock = DockStyle.Right,
                Width = 42,
                FlatStyle = FlatStyle.Flat,
                ForeColor = UiColors.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnMinimize.FlatAppearance.BorderSize = 0;
            btnMinimize.FlatAppearance.MouseOverBackColor = UiColors.ControlBg;
            btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;

            titleBar.Controls.Add(lblAppTitle);
            titleBar.Controls.Add(btnMinimize);
            titleBar.Controls.Add(btnClose);
            Controls.Add(titleBar);

            // 2. Bottom Status Bar
            Panel statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = UiColors.SidebarBg
            };
            _lblStatusLeft = new Label
            {
                Text = "● Đang phát",
                ForeColor = UiColors.AccentMint,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 7)
            };
            _lblStatusRight = new Label
            {
                Text = "RAM 31 MB • CPU 0,2% • GPU 1%",
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
                Width = 160,
                BackColor = UiColors.SidebarBg,
                Padding = new Padding(10, 15, 10, 10)
            };

            // Sidebar Menu Items
            Button btnNavLibrary = CreateSidebarButton("⊞  Thư viện", true);
            Button btnNavPlayback = CreateSidebarButton("▷  Phát lại", false);
            Button btnNavPerf = CreateSidebarButton("📈  Hiệu năng", false);
            Button btnNavSystem = CreateSidebarButton("☼  Hệ thống", false);

            Label lblVersion = new Label
            {
                Text = "Bản 0.1 • chạy nền ở khay\nhệ thống",
                ForeColor = UiColors.TextMuted,
                Font = new Font("Segoe UI", 8F),
                Dock = DockStyle.Bottom,
                Height = 38
            };

            sidebar.Controls.Add(btnNavSystem);
            sidebar.Controls.Add(btnNavPerf);
            sidebar.Controls.Add(btnNavPlayback);
            sidebar.Controls.Add(btnNavLibrary);
            sidebar.Controls.Add(lblVersion);
            Controls.Add(sidebar);

            // 4. Main Body Content
            Panel contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 15, 20, 10),
                BackColor = UiColors.Background
            };

            // --- A. Top Section (Preview Player & Details) ---
            Panel topSection = new Panel
            {
                Dock = DockStyle.Top,
                Height = 175
            };

            // A1. Preview Box (Left)
            RoundedPanel previewBox = new RoundedPanel
            {
                Width = 288,
                Height = 162,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(37, 48, 59),
                CornerRadius = 12
            };
            previewBox.Paint += DrawPreviewPlayIcon;

            _lblBadge = new Label
            {
                Text = "1920×1080 • 30 fps",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 19, 25, 30),
                AutoSize = true,
                Location = new Point(10, 133),
                Padding = new Padding(6, 3, 6, 3)
            };
            previewBox.Controls.Add(_lblBadge);

            // A2. Details & Actions (Right)
            Panel detailsPanel = new Panel
            {
                Location = new Point(308, 0),
                Width = 430,
                Height = 185
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
                Text = "Màn hình",
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiColors.TextSecondary,
                Location = new Point(0, 68),
                AutoSize = true
            };

            _cboMonitors = new ComboBox
            {
                Location = new Point(0, 90),
                Width = 380,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };

            _btnApply = new Button
            {
                Text = "Đặt làm nền",
                Location = new Point(0, 132),
                Width = 125,
                Height = 38,
                BackColor = UiColors.AccentLight,
                ForeColor = Color.FromArgb(17, 22, 27),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnApply.FlatAppearance.BorderSize = 0;
            _btnApply.Click += (s, e) => ApplyCurrentSelected();

            _btnOptimize = new Button
            {
                Text = "Tối ưu video",
                Location = new Point(135, 132),
                Width = 115,
                Height = 38,
                BackColor = UiColors.ControlBg,
                ForeColor = UiColors.TextPrimary,
                Font = new Font("Segoe UI", 9.5F),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnOptimize.FlatAppearance.BorderSize = 0;
            _btnOptimize.Click += (s, e) => MessageBox.Show("Tính năng tối ưu GPU hardware acceleration đã được bật tự động qua 'hwdec=auto'.", "Tối ưu Video", MessageBoxButtons.OK, MessageBoxIcon.Information);

            detailsPanel.Controls.Add(_lblTitle);
            detailsPanel.Controls.Add(_lblSubtitle);
            detailsPanel.Controls.Add(lblMonitorHeader);
            detailsPanel.Controls.Add(_cboMonitors);
            detailsPanel.Controls.Add(_btnApply);
            detailsPanel.Controls.Add(_btnOptimize);

            topSection.Controls.Add(previewBox);
            topSection.Controls.Add(detailsPanel);
            contentPanel.Controls.Add(topSection);

            // --- B. Middle Section: Thư viện Wallpaper Cards ---
            Panel libraryHeaderPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(0, 5, 0, 0)
            };
            Label lblLibTitle = new Label
            {
                Text = "Thư viện",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = UiColors.TextPrimary,
                Dock = DockStyle.Left,
                AutoSize = true
            };
            Label lblAddVideoLink = new Label
            {
                Text = "Thêm video",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = UiColors.AccentMint,
                Dock = DockStyle.Right,
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            lblAddVideoLink.Click += (s, e) => AddNewVideoDialog();

            libraryHeaderPanel.Controls.Add(lblLibTitle);
            libraryHeaderPanel.Controls.Add(lblAddVideoLink);
            contentPanel.Controls.Add(libraryHeaderPanel);

            _cardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 110,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 5, 0, 0)
            };
            contentPanel.Controls.Add(_cardsPanel);

            // --- C. Bottom Section: 2 Rounded Config Cards ---
            TableLayoutPanel bottomCardsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 10, 0, 0)
            };
            bottomCardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            bottomCardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // C1. Card Trái: "Phát lại"
            RoundedPanel cardPlayback = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 14, 18, 14),
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

            _chkMute = new ModernCheckBox
            {
                Text = "Tắt tiếng",
                Checked = _settings.IsMuted,
                Dock = DockStyle.Top,
                Height = 30
            };
            _chkMute.CheckedChanged += (s, e) => _wallpaperForm.SetMute(_chkMute.Checked);

            _chkLoop = new ModernCheckBox
            {
                Text = "Lặp vô tận",
                Checked = _settings.LoopInf,
                Dock = DockStyle.Top,
                Height = 30
            };
            _chkLoop.CheckedChanged += (s, e) => _wallpaperForm.SetLoop(_chkLoop.Checked);

            Panel volumeRow = new Panel { Dock = DockStyle.Top, Height = 32 };
            Label lblVol = new Label { Text = "Âm lượng", ForeColor = UiColors.TextPrimary, Location = new Point(0, 6), AutoSize = true };
            _sliderVolume = new ModernSlider
            {
                Location = new Point(120, 6),
                Width = 180,
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

            // C2. Card Phải: "Tự động tạm dừng khi"
            RoundedPanel cardAutoPause = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 14, 18, 14),
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

            _chkPauseFullscreen = new ModernCheckBox
            {
                Text = "Có ứng dụng toàn màn hình",
                Checked = _settings.PauseOnFullscreen,
                Dock = DockStyle.Top,
                Height = 30
            };
            _chkPauseFullscreen.CheckedChanged += (s, e) =>
            {
                _settings.PauseOnFullscreen = _chkPauseFullscreen.Checked;
                _settings.Save();
            };

            _chkPauseLock = new ModernCheckBox
            {
                Text = "Khóa hoặc tắt màn hình",
                Checked = _settings.PauseOnLock,
                Dock = DockStyle.Top,
                Height = 30
            };
            _chkPauseLock.CheckedChanged += (s, e) =>
            {
                _settings.PauseOnLock = _chkPauseLock.Checked;
                _settings.Save();
            };

            _chkPauseBattery = new ModernCheckBox
            {
                Text = "Chạy bằng pin",
                Checked = _settings.PauseOnBattery,
                Dock = DockStyle.Top,
                Height = 30
            };
            _chkPauseBattery.CheckedChanged += (s, e) =>
            {
                _settings.PauseOnBattery = _chkPauseBattery.Checked;
                if (_wallpaperForm.PauseManager != null)
                {
                    _wallpaperForm.PauseManager.PauseOnBattery = _chkPauseBattery.Checked;
                }
                _settings.Save();
            };

            cardAutoPause.Controls.Add(_chkPauseBattery);
            cardAutoPause.Controls.Add(_chkPauseLock);
            cardAutoPause.Controls.Add(_chkPauseFullscreen);
            cardAutoPause.Controls.Add(lblAutoPauseTitle);

            bottomCardsLayout.Controls.Add(cardPlayback, 0, 0);
            bottomCardsLayout.Controls.Add(cardAutoPause, 1, 0);
            contentPanel.Controls.Add(bottomCardsLayout);

            Controls.Add(contentPanel);
        }

        private Button CreateSidebarButton(string text, bool isActive)
        {
            Button btn = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 42,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, isActive ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isActive ? UiColors.AccentLight : UiColors.TextSecondary,
                BackColor = isActive ? Color.FromArgb(31, 45, 53) : Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void DrawPreviewPlayIcon(object? sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Vòng tròn play ở tâm
            int cx = 144;
            int cy = 81;
            int radius = 24;

            using SolidBrush circleBrush = new SolidBrush(Color.FromArgb(160, 24, 32, 38));
            e.Graphics.FillEllipse(circleBrush, cx - radius, cy - radius, radius * 2, radius * 2);

            // Tam giác Play
            Point[] triangle = new Point[]
            {
                new Point(cx - 5, cy - 9),
                new Point(cx + 9, cy),
                new Point(cx - 5, cy + 9)
            };
            using SolidBrush playBrush = new SolidBrush(Color.White);
            e.Graphics.FillPolygon(playBrush, triangle);
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

            // Đảm bảo có ít nhất các wallpaper mẫu trong danh sách
            EnsureSampleWallpapers();

            Color[] colors = new Color[]
            {
                Color.FromArgb(50, 70, 95),   // Neon Rain (Blue/Slate)
                Color.FromArgb(40, 75, 65),   // Forest Mist (Green)
                Color.FromArgb(70, 55, 85),   // Code Rain (Purple)
                Color.FromArgb(90, 70, 55),   // Sunset Loop (Orange/Brown)
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
                _cardsPanel.Controls.Add(card);
            }

            // Thêm card [+] "Thêm mới" ở cuối
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
            // Các video có sẵn trên máy
            string[] samples = new string[]
            {
                @"D:\.Web_\Codde\christmas_tree_ascii.mp4",
                @"D:\.Web_\Codde\newyear_fireworks_ascii.mp4",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "particles.html")
            };

            foreach (string s in samples)
            {
                if (File.Exists(s) && !_settings.VideoLibrary.Contains(s))
                {
                    _settings.VideoLibrary.Add(s);
                }
            }

            if (string.IsNullOrEmpty(_selectedWallpaperPath) && _settings.VideoLibrary.Count > 0)
            {
                _selectedWallpaperPath = _settings.VideoLibrary[0];
            }
        }

        private void SelectWallpaperCard(WallpaperCardControl clickedCard)
        {
            _selectedWallpaperPath = clickedCard.FilePath;

            foreach (Control c in _cardsPanel.Controls)
            {
                if (c is WallpaperCardControl card && !card.IsAddCard)
                {
                    card.IsSelected = (card == clickedCard);
                    card.Invalidate();
                }
            }

            UpdateDetailsView(_selectedWallpaperPath);
        }

        private void UpdateDetailsView(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                _lblTitle.Text = "Chưa chọn hình nền";
                _lblSubtitle.Text = "Chọn một thẻ hình nền ở bên dưới";
                return;
            }

            if (path.StartsWith("http"))
            {
                _lblTitle.Text = "Web Wallpaper";
                _lblSubtitle.Text = $"{path} • HTML5/WebGL";
                _lblBadge.Text = "Web • Online";
            }
            else if (File.Exists(path))
            {
                FileInfo fi = new FileInfo(path);
                double mb = fi.Length / (1024.0 * 1024.0);
                string ext = fi.Extension.TrimStart('.').ToUpper();

                _lblTitle.Text = Path.GetFileNameWithoutExtension(fi.Name);
                _lblSubtitle.Text = $"{fi.Name} • {ext} • {mb:F1} MB";

                var screens = Screen.AllScreens;
                _lblBadge.Text = $"{screens[0].Bounds.Width}×{screens[0].Bounds.Height} • 60 fps";
            }
            else
            {
                _lblTitle.Text = Path.GetFileNameWithoutExtension(path);
                _lblSubtitle.Text = path;
            }
        }

        private void ApplyCurrentSelected()
        {
            if (string.IsNullOrEmpty(_selectedWallpaperPath)) return;

            _wallpaperForm.ChangeSource(_selectedWallpaperPath);
            _settings.CurrentVideoPath = _selectedWallpaperPath;
            _settings.Save();

            _lblStatusLeft.Text = "● Đang phát";
            _lblStatusLeft.ForeColor = UiColors.AccentMint;

            MessageBox.Show($"Đã áp dụng hình nền thành công:\n{_lblTitle.Text}", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AddNewVideoDialog()
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Chọn file video hoặc trang web HTML",
                Filter = "Wallpaper Files (*.mp4;*.mkv;*.webm;*.avi;*.html)|*.mp4;*.mkv;*.webm;*.avi;*.html|All Files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (string file in ofd.FileNames)
                {
                    if (!_settings.VideoLibrary.Contains(file))
                    {
                        _settings.VideoLibrary.Add(file);
                    }
                }

                _selectedWallpaperPath = ofd.FileNames[0];
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

                _lblStatusRight.Text = $"RAM {ram:F0} MB • CPU {cpu:F1}% • GPU 1%";
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
            base.OnFormClosing(e);
        }
    }
}
