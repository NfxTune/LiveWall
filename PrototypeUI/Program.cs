using LiveWall.UI;

ApplicationConfiguration.Initialize();

var form = new MainForm(AppSettings.Load());

// Chỗ nối với phần lõi (M1/M2): thay MessageBox bằng gọi hook desktop + libmpv.
form.ApplyRequested += (path, monitor) =>
    MessageBox.Show(form,
        $"Phát: {path}\nMàn hình: {(monitor < 0 ? "tất cả" : (monitor + 1).ToString())}",
        "LiveWall");

form.SettingsChanged += s =>
    System.Diagnostics.Debug.WriteLine($"mute={s.Mute} loop={s.Loop} vol={s.Volume} fullscreen={s.PauseFullscreen}");

Application.Run(form);
