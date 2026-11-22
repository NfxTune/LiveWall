using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LiveWall.UI;

/// <summary>Cửa sổ cấu hình LiveWall (880×560, nền tối, không viền).</summary>
public sealed class MainForm : Form
{
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

    // ---- Sự kiện để nối với phần lõi (hook desktop + libmpv) ----
    /// <summary>Bấm "Đặt làm nền": (đường dẫn video, chỉ số màn hình; -1 = trải dài).</summary>
    public event Action<string, int>? ApplyRequested;
    /// <summary>Bất kỳ công tắc/slider nào đổi giá trị.</summary>
    public event Action<AppSettings>? SettingsChanged;
    /// <summary>Bấm mục ở thanh bên (Phát lại, Hiệu năng, Hệ thống): bạn tự xử lý.</summary>
    public event Action<string>? NavigationRequested;
    /// <summary>Yêu cầu thoát ứng dụng, cho phép phần lõi dọn tài nguyên trước khi đóng.</summary>
    public event Action? ExitRequested;
    /// <summary>Bật hoặc tạm dừng hình nền hiện tại.</summary>
    public event Action? PlaybackToggleRequested;

    /// <summary>true = bấm X chỉ ẩn cửa sổ (dùng khi đã có tray icon).</summary>
    public bool HideOnClose { get; set; }

    readonly AppSettings _s;
    readonly PreviewBox _preview = new();
    readonly Label _title;
    readonly Label _meta;
    readonly ComboBox _screen = new();
    readonly RoundedButton _apply = new();
    readonly RoundedButton _optimize = new();
    readonly RoundedButton _togglePlayback = new();
    readonly FlowLayoutPanel _flow = new();
    readonly AccentCheck _mute = new(), _loop = new(), _pFull = new(), _pLock = new(), _pBattery = new();
    readonly TrackBar _volume = new();
    readonly Panel _status = new();
    readonly Panel _pageHost = new();
    readonly List<(string Name, NavItem Item)> _navItems = new();
    readonly Label _perfSummary = new();
    readonly AccentCheck _autoStart = new();
    readonly List<Panel> _pages = new();
    string _currentPage = "Thư viện";
    bool _syncingSettings;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };

    string? _selected;
    bool _playing = true;
    string _stateText = "Đang phát";
    string _statsText = "";
    TimeSpan _lastCpu;
    DateTime _lastTick;

    public MainForm(AppSettings settings)
    {
        _s = settings;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(880, 560);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        Text = "LiveWall";
        DoubleBuffered = true;

        string appIco = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (!File.Exists(appIco)) appIco = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(appIco))
        {
            try { Icon = new Icon(appIco); } catch { }
        }

        _title = Lbl("", Theme.H1, Theme.Text, 328, 20, 364, 30);
        _meta = Lbl("", Theme.Small, Theme.Muted, 328, 50, 364, 20);

        // Dock: control Fill thêm đầu tiên, các control dock cạnh thêm sau.
        Controls.Add(BuildContent());
        Controls.Add(BuildSidebar());
        Controls.Add(BuildStatusBar());
        Controls.Add(BuildTitleBar());
        BuildSecondaryPages();

        _timer.Tick += (_, _) => UpdateStats();

        ResumeLayout(false);

        _selected = _s.Current != null && _s.Library.Contains(_s.Current) ? _s.Current : _s.Library.FirstOrDefault();
        RebuildLibrary();
        UpdatePreview();
    }

    // =====================================================================
    //  API công khai cho phần lõi
    // =====================================================================

    /// <summary>Cập nhật chấm trạng thái ở thanh dưới, ví dụ SetPlaybackState(false, "game toàn màn hình").</summary>
    public void SetPlaybackState(bool playing, string? reason = null)
    {
        _playing = playing;
        _stateText = playing ? "Đang phát" : reason is null ? "Tạm dừng" : $"Tạm dừng · {reason}";
        _togglePlayback.Text = playing ? "Tạm dừng" : "Tiếp tục";
        _status.Invalidate();
    }

    public void SetAutoStartState(bool enabled)
    {
        _s.AutoStart = enabled;
        _syncingSettings = true;
        try { _autoStart.Checked = enabled; }
        finally { _syncingSettings = false; }
    }

    // =====================================================================
    //  Dựng giao diện
    // =====================================================================

    static Label Lbl(string text, Font font, Color color, int x, int y, int w, int h) => new()
    {
        Text = text, Font = font, ForeColor = color,
        Location = new Point(x, y), Size = new Size(w, h),
        TextAlign = ContentAlignment.MiddleLeft
    };

    Panel BuildTitleBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.TitleBar };

        var logo = new PictureBox
        {
            Location = new Point(10, 8),
            Size = new Size(20, 20),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "LiveWallLogo.png");
        if (File.Exists(logoPath)) logo.Image = new Bitmap(logoPath);
        else
        {
            using var fallbackIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (fallbackIcon != null) logo.Image = fallbackIcon.ToBitmap();
        }
        logo.Disposed += (_, _) => logo.Image?.Dispose();
        var name = Lbl("LiveWall", Theme.Small, Theme.Muted, 34, 0, 200, 36);

        var close = new CaptionButton { IsClose = true, Dock = DockStyle.Right, Width = 46 };
        var min = new CaptionButton { IsClose = false, Dock = DockStyle.Right, Width = 46 };
        min.Click += (_, _) => WindowState = FormWindowState.Minimized;
        close.Click += (_, _) => { if (HideOnClose) Hide(); else Close(); };

        foreach (Control c in new Control[] { bar, logo, name })
            c.MouseDown += DragWindow;

        bar.Controls.AddRange(new Control[] { logo, name, min, close });
        return bar;
    }

    Panel BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = 168, BackColor = Theme.Side };
        var items = new[] { ("Thư viện", "\uE8F1"), ("Phát lại", "\uE768"), ("Hiệu năng", "\uE9D9"), ("Hệ thống", "\uE713") };

        for (int i = 0; i < items.Length; i++)
        {
            var (text, glyph) = items[i];
            var nav = new NavItem { Text = text, Glyph = glyph, Active = i == 0, Location = new Point(12, 12 + i * 40) };
            _navItems.Add((text, nav));
            nav.Click += (_, _) => Navigate(text);
            side.Controls.Add(nav);
        }

        side.Controls.Add(Lbl("Bản 0.1 · chạy nền ở khay hệ thống", Theme.Tiny, Theme.Muted, 16, 452, 140, 34));
        return side;
    }

    Panel BuildStatusBar()
    {
        _status.Dock = DockStyle.Bottom;
        _status.Height = 28;
        _status.BackColor = Theme.TitleBar;
        typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(_status, true);

        _status.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(_playing ? Theme.Accent : Theme.Muted)) g.FillEllipse(b, 16, 10, 8, 8);
            TextRenderer.DrawText(g, _stateText, Theme.Small, new Rectangle(30, 0, 400, 28), Theme.Muted, Theme.TxtLeft);
            TextRenderer.DrawText(g, _statsText, Theme.Small, new Rectangle(_status.Width - 416, 0, 400, 28), Theme.Muted, Theme.TxtRight);
        };
        return _status;
    }

    Panel BuildContent()
    {
        _pageHost.Dock = DockStyle.Fill;
        _pageHost.BackColor = Theme.Bg;
        var c = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Tag = "Thư viện" };

        // ---- Hàng 1: xem trước + thông tin + nút ----
        _preview.Location = new Point(20, 20);
        c.Controls.Add(_preview);
        c.Controls.Add(_title);
        c.Controls.Add(_meta);

        c.Controls.Add(Lbl("Màn hình", Theme.Small, Theme.Muted, 328, 80, 200, 18));
        _screen.Location = new Point(328, 100);
        _screen.Size = new Size(364, 28);
        _screen.DropDownStyle = ComboBoxStyle.DropDownList;
        _screen.FlatStyle = FlatStyle.Flat;
        _screen.BackColor = Theme.Card;
        _screen.ForeColor = Theme.Text;
        _screen.Font = Theme.Body;
        FillScreens();
        _screen.SelectedIndexChanged += (_, _) =>
        {
            _s.MonitorIndex = _screen.SelectedIndex >= Screen.AllScreens.Length ? -1 : _screen.SelectedIndex;
            Changed();
        };
        c.Controls.Add(_screen);

        _apply.Text = "Đặt làm nền";
        _apply.Style = ButtonStyle.Primary;
        _apply.Location = new Point(328, 148);
        _apply.Size = new Size(120, 34);
        _apply.Click += (_, _) => ApplyClicked();
        c.Controls.Add(_apply);

        _optimize.Text = "Tối ưu video";
        _optimize.Style = ButtonStyle.Outline;
        _optimize.Location = new Point(456, 148);
        _optimize.Size = new Size(120, 34);
        _optimize.Click += async (_, _) => await OptimizeClickedAsync();
        c.Controls.Add(_optimize);

        _togglePlayback.Text = "Tạm dừng";
        _togglePlayback.Style = ButtonStyle.Outline;
        _togglePlayback.Location = new Point(584, 148);
        _togglePlayback.Size = new Size(108, 34);
        _togglePlayback.Click += (_, _) =>
        {
            if (PlaybackToggleRequested is null) SetPlaybackState(!_playing);
            else PlaybackToggleRequested.Invoke();
        };
        c.Controls.Add(_togglePlayback);

        // ---- Hàng 2: thư viện ----
        c.Controls.Add(Lbl("Thư viện", Theme.H3, Theme.Text, 20, 198, 200, 24));
        var add = new RoundedButton { Text = "Thêm video", Style = ButtonStyle.Link, Location = new Point(602, 198), Size = new Size(90, 24) };
        add.Click += (_, _) => AddVideos();
        c.Controls.Add(add);

        _flow.Location = new Point(20, 226);
        _flow.Size = new Size(672, 108);
        _flow.WrapContents = false;
        _flow.AutoScroll = true;
        _flow.BackColor = Theme.Bg;
        c.Controls.Add(_flow);

        // ---- Hàng 3: hai thẻ cài đặt ----
        var play = MakeCard("Phát lại", 20);
        _mute.Text = "Tắt tiếng";        _mute.Location = new Point(14, 40);
        _loop.Text = "Lặp vô tận";       _loop.Location = new Point(14, 70);
        play.Controls.AddRange(new Control[] { _mute, _loop });
        play.Controls.Add(Lbl("Âm lượng", Theme.Body, Theme.Text, 14, 100, 110, 22));
        _volume.Location = new Point(140, 98);
        _volume.Size = new Size(174, 28);
        _volume.AutoSize = false;
        _volume.TickStyle = TickStyle.None;
        _volume.Minimum = 0; _volume.Maximum = 100; _volume.SmallChange = 5; _volume.LargeChange = 10;
        _volume.BackColor = Theme.Card;
        _volume.Tag = "Volume";
        play.Controls.Add(_volume);

        var pause = MakeCard("Tự động tạm dừng khi", 364);
        _pFull.Text = "Có ứng dụng toàn màn hình"; _pFull.Location = new Point(14, 40);
        _pLock.Text = "Khóa hoặc tắt màn hình";    _pLock.Location = new Point(14, 70);
        _pBattery.Text = "Chạy bằng pin";          _pBattery.Location = new Point(14, 100);
        pause.Controls.AddRange(new Control[] { _pFull, _pLock, _pBattery });

        c.Controls.Add(play);
        c.Controls.Add(pause);

        BindSettings();
        _pageHost.Controls.Add(c);
        _pages.Add(c);
        return _pageHost;
    }

    void Navigate(string name)
    {
        _currentPage = name;
        foreach (var (pageName, item) in _navItems) { item.Active = pageName == name; item.Invalidate(); }
        foreach (var page in _pages) page.Visible = (string?)page.Tag == name;
        var selected = _pages.FirstOrDefault(p => (string?)p.Tag == name);
        if (selected != null) selected.BringToFront();
        if (name == "Hiệu năng") UpdatePerformancePage();
        NavigationRequested?.Invoke(name);
    }

    void BuildSecondaryPages()
    {
        var playback = CreateSettingsPage("Phát lại", "Điều khiển âm thanh và hành vi phát hình nền.");
        AddPageCheck(playback, "Tắt tiếng", _s.Mute, 94, value => { _s.Mute = value; Changed(); }, "Mute");
        AddPageCheck(playback, "Lặp video vô tận", _s.Loop, 132, value => { _s.Loop = value; Changed(); }, "Loop");
        playback.Controls.Add(Lbl("Âm lượng", Theme.Body, Theme.Text, 30, 174, 120, 24));
        var volume = new TrackBar { Minimum = 0, Maximum = 100, Value = Math.Clamp(_s.Volume, 0, 100), TickStyle = TickStyle.None, Location = new Point(145, 164), Size = new Size(300, 42), BackColor = Theme.Bg };
        var volumeValue = Lbl($"{volume.Value}%", Theme.Body, Theme.Muted, 455, 174, 60, 24);
        volumeValue.Tag = "VolumeValue";
        volume.Tag = "Volume";
        volume.ValueChanged += (_, _) => { if (_syncingSettings) return; _s.Volume = volume.Value; SyncSettingsControls(); SettingsChanged?.Invoke(_s); };
        volume.MouseUp += (_, _) => SaveSettings(); volume.KeyUp += (_, _) => SaveSettings();
        playback.Controls.Add(volume); playback.Controls.Add(volumeValue);
        AddPageCheck(playback, "Tạm dừng khi có ứng dụng toàn màn hình", _s.PauseFullscreen, 222, value => { _s.PauseFullscreen = value; Changed(); }, "PauseFullscreen");
        AddPageCheck(playback, "Tạm dừng khi khóa hoặc tắt màn hình", _s.PauseLock, 260, value => { _s.PauseLock = value; Changed(); }, "PauseLock");
        AddPageCheck(playback, "Tạm dừng khi chạy bằng pin", _s.PauseBattery, 298, value => { _s.PauseBattery = value; Changed(); }, "PauseBattery");
        AddPage(playback);

        var performance = CreateSettingsPage("Hiệu năng", "Theo dõi tài nguyên của LiveWall và giảm tải khi bạn đang làm việc.");
        _perfSummary.Font = Theme.Body; _perfSummary.ForeColor = Theme.Text; _perfSummary.Location = new Point(30, 95); _perfSummary.Size = new Size(560, 70);
        performance.Controls.Add(_perfSummary);
        AddPageCheck(performance, "Tạm dừng khi có ứng dụng toàn màn hình", _s.PauseFullscreen, 190, value => { _s.PauseFullscreen = value; Changed(); }, "PauseFullscreen");
        AddPageCheck(performance, "Tạm dừng khi khóa hoặc tắt màn hình", _s.PauseLock, 228, value => { _s.PauseLock = value; Changed(); }, "PauseLock");
        AddPageCheck(performance, "Tạm dừng khi chạy bằng pin", _s.PauseBattery, 266, value => { _s.PauseBattery = value; Changed(); }, "PauseBattery");
        AddPage(performance);

        var system = CreateSettingsPage("Hệ thống", "Tùy chỉnh cách LiveWall khởi động và quản lý dữ liệu.");
        _autoStart.Text = "Khởi động LiveWall cùng Windows"; _autoStart.Checked = LiveWallpaper.AutoStartManager.IsEnabled();
        _autoStart.Location = new Point(30, 95); _autoStart.Size = new Size(400, 26);
        _autoStart.CheckedChanged += (_, _) =>
        {
            if (_syncingSettings) return;
            if (!LiveWallpaper.AutoStartManager.SetAutoStart(_autoStart.Checked))
            {
                MessageBox.Show(this, "Không thể cập nhật thiết lập khởi động Windows.", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _autoStart.Checked = LiveWallpaper.AutoStartManager.IsEnabled();
                return;
            }
            _s.AutoStart = _autoStart.Checked;
            SaveSettings();
        };
        system.Controls.Add(_autoStart);
        system.Controls.Add(Lbl("Tệp cài đặt", Theme.Body, Theme.Text, 30, 150, 180, 24));
        string settingsPath = _s.Persist is null ? AppSettings.FilePath : LiveWallpaper.AppSettings.SettingsFilePath;
        system.Controls.Add(Lbl(settingsPath, Theme.Tiny, Theme.Muted, 30, 176, 620, 24));
        var openSettings = new RoundedButton { Text = "Mở thư mục cấu hình", Style = ButtonStyle.Outline, Location = new Point(30, 214), Size = new Size(190, 34) };
        openSettings.Click += (_, _) => OpenFolder(Path.GetDirectoryName(settingsPath)!);
        system.Controls.Add(openSettings);
        var openLog = new RoundedButton { Text = "Mở nhật ký ứng dụng", Style = ButtonStyle.Outline, Location = new Point(232, 214), Size = new Size(190, 34) };
        openLog.Click += (_, _) => OpenFolder(Path.Combine(AppContext.BaseDirectory, "logs"));
        system.Controls.Add(openLog);
        var reset = new RoundedButton { Text = "Khôi phục cài đặt phát lại", Style = ButtonStyle.Link, Location = new Point(30, 278), Size = new Size(220, 30) };
        reset.Click += (_, _) => ResetPlaybackSettings();
        system.Controls.Add(reset);
        var shortcut = new RoundedButton { Text = "Tạo shortcut Start Menu", Style = ButtonStyle.Outline, Location = new Point(270, 278), Size = new Size(190, 30) };
        shortcut.Click += (_, _) =>
        {
            if (LiveWallpaper.ShortcutManager.TryCreateStartMenuShortcut(out string shortcutPath))
                MessageBox.Show(this, $"Đã tạo lối tắt để mở LiveWall:\n{shortcutPath}", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(this, "Không tạo được lối tắt Start Menu. Hãy xem nhật ký ứng dụng để biết thêm chi tiết.", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        system.Controls.Add(shortcut);
        var exit = new RoundedButton { Text = "Thoát LiveWall", Style = ButtonStyle.Outline, Location = new Point(30, 326), Size = new Size(150, 36) };
        exit.Click += (_, _) =>
        {
            if (ExitRequested is null) Close();
            else ExitRequested.Invoke();
        };
        system.Controls.Add(exit);
        AddPage(system);
    }

    Panel CreateSettingsPage(string title, string subtitle)
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Tag = title };
        page.Controls.Add(Lbl(title, Theme.H1, Theme.Text, 24, 22, 560, 32));
        page.Controls.Add(Lbl(subtitle, Theme.Small, Theme.Muted, 24, 58, 640, 24));
        return page;
    }

    void AddPage(Panel page) { _pageHost.Controls.Add(page); _pages.Add(page); page.Visible = false; }

    static void AddPageCheck(Panel page, string text, bool initial, int y, Action<bool> changed, string settingKey)
    {
        var check = new AccentCheck { Text = text, Checked = initial, Tag = settingKey, Location = new Point(30, y), Size = new Size(500, 24) };
        check.CheckedChanged += (_, _) => { if (page.FindForm() is MainForm form && !form._syncingSettings) changed(check.Checked); };
        page.Controls.Add(check);
    }

    void UpdatePerformancePage()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        _perfSummary.Text = $"Bộ nhớ ứng dụng: {process.WorkingSet64 / 1048576} MB\nCPU: xem mức sử dụng trực tiếp ở thanh trạng thái phía dưới.";
    }

    static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    void ResetPlaybackSettings()
    {
        _s.Mute = true; _s.Loop = true; _s.Volume = 40;
        _s.PauseFullscreen = true; _s.PauseLock = true; _s.PauseBattery = false;
        SyncSettingsControls();
        SaveSettings();
        MessageBox.Show(this, "Đã khôi phục các tùy chọn phát lại mặc định. Thay đổi đã được áp dụng.", "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static Card MakeCard(string title, int x)
    {
        var card = new Card { Location = new Point(x, 334), Size = new Size(328, 142) };
        card.Controls.Add(Lbl(title, Theme.Semibold, Theme.Text, 14, 10, 300, 22));
        return card;
    }

    void FillScreens()
    {
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
            _screen.Items.Add($"Màn hình {i + 1} ({screens[i].Bounds.Width}×{screens[i].Bounds.Height})");
        _screen.Items.Add("Tất cả màn hình (trải dài)");

        _screen.SelectedIndex = _s.MonitorIndex < 0 ? screens.Length : Math.Min(_s.MonitorIndex, screens.Length - 1);
    }

    /// <summary>Nạp giá trị từ settings rồi mới gắn sự kiện, tránh ghi file thừa lúc khởi động.</summary>
    void BindSettings()
    {
        _mute.Checked = _s.Mute;
        _loop.Checked = _s.Loop;
        _volume.Value = Math.Clamp(_s.Volume, 0, 100);
        _volume.Enabled = !_s.Mute;
        _pFull.Checked = _s.PauseFullscreen;
        _pLock.Checked = _s.PauseLock;
        _pBattery.Checked = _s.PauseBattery;

        _mute.Tag = "Mute"; _loop.Tag = "Loop"; _pFull.Tag = "PauseFullscreen"; _pLock.Tag = "PauseLock"; _pBattery.Tag = "PauseBattery";
        _mute.CheckedChanged += (_, _) => { if (_syncingSettings) return; _s.Mute = _mute.Checked; _volume.Enabled = !_mute.Checked; Changed(); };
        _loop.CheckedChanged += (_, _) => { if (!_syncingSettings) { _s.Loop = _loop.Checked; Changed(); } };
        _pFull.CheckedChanged += (_, _) => { if (!_syncingSettings) { _s.PauseFullscreen = _pFull.Checked; Changed(); } };
        _pLock.CheckedChanged += (_, _) => { if (!_syncingSettings) { _s.PauseLock = _pLock.Checked; Changed(); } };
        _pBattery.CheckedChanged += (_, _) => { if (!_syncingSettings) { _s.PauseBattery = _pBattery.Checked; Changed(); } };

        // Kéo slider: báo ngay để chỉnh âm lượng trực tiếp, chỉ ghi file khi thả chuột/phím.
        _volume.ValueChanged += (_, _) => { if (_syncingSettings) return; _s.Volume = _volume.Value; SyncSettingsControls(); SettingsChanged?.Invoke(_s); };
        _volume.MouseUp += (_, _) => SaveSettings();
        _volume.KeyUp += (_, _) => SaveSettings();
    }

    void Changed()
    {
        SyncSettingsControls();
        SaveSettings();
    }

    void SyncSettingsControls()
    {
        _syncingSettings = true;
        try
        {
            foreach (var check in _pageHost.Controls.OfType<Panel>().SelectMany(p => FindControls<AccentCheck>(p)))
            {
                check.Checked = check.Tag?.ToString() switch
                {
                    "Mute" => _s.Mute,
                    "Loop" => _s.Loop,
                    "PauseFullscreen" => _s.PauseFullscreen,
                    "PauseLock" => _s.PauseLock,
                    "PauseBattery" => _s.PauseBattery,
                    _ => check.Checked
                };
            }
            foreach (var slider in _pageHost.Controls.OfType<Panel>().SelectMany(p => FindControls<TrackBar>(p)).Where(t => Equals(t.Tag, "Volume")))
            {
                slider.Value = Math.Clamp(_s.Volume, slider.Minimum, slider.Maximum);
                slider.Enabled = !_s.Mute;
            }
            foreach (var label in _pageHost.Controls.OfType<Panel>().SelectMany(p => FindControls<Label>(p)).Where(l => Equals(l.Tag, "VolumeValue")))
                label.Text = $"{_s.Volume}%";
            _volume.Value = Math.Clamp(_s.Volume, 0, 100);
            _volume.Enabled = !_s.Mute;
        }
        finally { _syncingSettings = false; }
    }

    static IEnumerable<T> FindControls<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match) yield return match;
            foreach (var nested in FindControls<T>(child)) yield return nested;
        }
    }

    void SaveSettings()
    {
        _s.Save();
        SettingsChanged?.Invoke(_s);
    }

    // =====================================================================
    //  Thư viện video
    // =====================================================================

    void RebuildLibrary()
    {
        _flow.SuspendLayout();
        foreach (var old in _flow.Controls.Cast<Control>().ToList())
        {
            _flow.Controls.Remove(old);
            old.Dispose();
        }

        foreach (var path in _s.Library)
        {
            var tile = new Tile
            {
                Title = Path.GetFileNameWithoutExtension(path),
                Tag = path,
                Selected = path == _selected,
                Fill = Theme.TilePalette[path.Sum(ch => ch) % Theme.TilePalette.Length],
                Margin = new Padding(0, 0, 12, 0),
                Thumb = Tools.LoadThumb(path)
            };
            tile.Click += (_, _) => SelectVideo(path);

            var menu = new ContextMenuStrip();
            menu.Items.Add("Xóa khỏi thư viện", null, (_, _) => RemoveVideo(path));
            tile.ContextMenuStrip = menu;
            tile.Disposed += (_, _) => menu.Dispose();

            _flow.Controls.Add(tile);
            if (tile.Thumb == null) _ = MakeThumbAsync(tile, path);
        }

        var addTile = new Tile { IsAdd = true, Title = "Thêm mới", Margin = new Padding(0, 0, 12, 0) };
        addTile.Click += (_, _) => AddVideos();
        _flow.Controls.Add(addTile);
        _flow.ResumeLayout();
    }

    async Task MakeThumbAsync(Tile tile, string path)
    {
        try
        {
            await Tools.EnsureThumbAsync(path);
            if (tile.IsDisposed) return;
            tile.Thumb = Tools.LoadThumb(path);
            if (path == _selected) UpdatePreview();
        }
        catch { /* không có thumbnail thì giữ ô màu */ }
    }

    void SelectVideo(string path)
    {
        _selected = path;
        _s.Current = path;
        SaveSettings();
        foreach (var t in _flow.Controls.OfType<Tile>().Where(t => !t.IsAdd))
        {
            t.Selected = (string?)t.Tag == path;
            t.Invalidate();
        }
        UpdatePreview();
    }

    void AddVideos()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Chọn video làm hình nền",
            Filter = "Video và ảnh động|*.mp4;*.mkv;*.webm;*.mov;*.avi;*.gif|Tất cả tệp|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        foreach (var f in dlg.FileNames)
            if (!_s.Library.Contains(f, StringComparer.OrdinalIgnoreCase)) _s.Library.Add(f);

        _selected = dlg.FileNames[0];
        _s.Current = _selected;
        SaveSettings();
        RebuildLibrary();
        UpdatePreview();
    }

    void RemoveVideo(string path)
    {
        _s.Library.Remove(path);
        if (_selected == path)
        {
            _selected = _s.Library.FirstOrDefault();
            _s.Current = _selected;
        }
        SaveSettings();
        RebuildLibrary();
        UpdatePreview();
    }

    async void UpdatePreview()
    {
        var path = _selected;
        if (path == null || !File.Exists(path))
        {
            _title.Text = path == null ? "Chưa chọn video" : "Không tìm thấy tệp";
            _meta.Text = path == null ? "Bấm “Thêm video” để bắt đầu" : path;
            _preview.Chip = "";
            _preview.SetVideo(null, false);
            _apply.Enabled = _optimize.Enabled = false;
            return;
        }

        var mb = new FileInfo(path).Length / 1048576.0;
        _title.Text = Path.GetFileNameWithoutExtension(path);
        _meta.Text = $"{Path.GetFileName(path)} · {mb:0.#} MB";
        _preview.Chip = "";
        _preview.SetVideo(Tools.LoadThumb(path), true);
        _apply.Enabled = _optimize.Enabled = true;

        var info = await Tools.ProbeAsync(path);
        if (info == null || path != _selected || IsDisposed) return; // đã đổi video khác trong lúc chờ

        _preview.Chip = info.ToString();
        _meta.Text = $"{Path.GetFileName(path)} · {info.Codec.ToUpperInvariant()} · {mb:0.#} MB";
        _preview.Invalidate();
    }

    // =====================================================================
    //  Hành động
    // =====================================================================

    void ApplyClicked()
    {
        if (_selected == null) return;
        _s.Active = _selected;
        SaveSettings();
        ApplyRequested?.Invoke(_selected, _s.MonitorIndex);
        SetPlaybackState(true);
    }

    async Task OptimizeClickedAsync()
    {
        if (_selected == null) return;

        _optimize.Enabled = false;
        _optimize.Text = "Đang tối ưu…";
        var output = await Tools.OptimizeAsync(_selected);
        _optimize.Text = "Tối ưu video";
        _optimize.Enabled = true;

        if (output == null)
        {
            MessageBox.Show(this,
                "Không tối ưu được video. Hãy đặt ffmpeg.exe vào thư mục tools\\ cạnh ứng dụng hoặc thêm ffmpeg vào PATH.",
                "LiveWall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_s.Library.Contains(output, StringComparer.OrdinalIgnoreCase)) _s.Library.Add(output);
        _selected = output;
        _s.Current = output;
        SaveSettings();
        RebuildLibrary();
        UpdatePreview();
    }

    // =====================================================================
    //  Thanh trạng thái: RAM/CPU của chính app (chỉ chạy khi cửa sổ đang hiện)
    // =====================================================================

    void UpdateStats()
    {
        using var p = Process.GetCurrentProcess();
        var now = DateTime.UtcNow;
        var cpu = p.TotalProcessorTime;
        double elapsedMs = Math.Max(1, (now - _lastTick).TotalMilliseconds * Environment.ProcessorCount);
        double pct = (cpu - _lastCpu).TotalMilliseconds / elapsedMs * 100;
        _lastCpu = cpu;
        _lastTick = now;

        _statsText = $"RAM {p.WorkingSet64 / 1048576} MB · CPU {pct:0.0}%";
        if (_currentPage == "Hiệu năng") _perfSummary.Text = $"Tài nguyên ứng dụng hiện tại\n{_statsText}";
        _status.Invalidate();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
        {
            using var p = Process.GetCurrentProcess();
            _lastCpu = p.TotalProcessorTime;
            _lastTick = DateTime.UtcNow;
            UpdateStats();
            _timer.Start();
        }
        else _timer.Stop();
    }

    // =====================================================================
    //  Cửa sổ không viền: kéo thả, góc bo Windows 11
    // =====================================================================

    void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, 0);
    }

        protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2;
        DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
    }

    private static readonly uint _msgActivateInstance = LiveWallpaper.NativeMethods.RegisterWindowMessage("LiveWall_ActivateMainWindow");

    protected override void WndProc(ref Message m)
    {
        if (_msgActivateInstance != 0 && m.Msg == (int)_msgActivateInstance)
        {
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
