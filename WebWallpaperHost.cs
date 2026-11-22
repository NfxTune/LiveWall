using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace LiveWallpaper
{
    public static class WebWallpaperHost
    {
        public static void Run(string initialTarget, string pipeName, int monitorIndex = 0)
        {
            Logger.Info($"[WebHost Process] Starting WebWallpaperHost. Target: '{initialTarget}', Pipe: '{pipeName}'");

            DesktopManager desktopManager = new DesktopManager();
            if (!desktopManager.InitializeWorkerW())
            {
                Logger.Error("[WebHost Process] Failed to initialize WorkerW!");
                return;
            }

            using WebWallpaperForm form = new WebWallpaperForm(desktopManager, initialTarget, pipeName, monitorIndex);
            Application.Run(form);

            Logger.Info("[WebHost Process] WebWallpaperHost exited.");
        }
    }

    public class WebWallpaperForm : Form
    {
        private readonly DesktopManager _desktopManager;
        private readonly string _initialTarget;
        private readonly string _pipeName;
        private WebView2? _webView;
        private CancellationTokenSource? _cts;

        public WebWallpaperForm(DesktopManager desktopManager, string initialTarget, string pipeName, int monitorIndex)
        {
            _desktopManager = desktopManager;
            _initialTarget = initialTarget;
            _pipeName = pipeName;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;

            Rectangle[] monitorBounds = Array.ConvertAll(Screen.AllScreens, screen => screen.Bounds);
            Rectangle bounds;
            if (monitorBounds.Length == 0)
            {
                bounds = new Rectangle(0, 0, 1920, 1080);
            }
            else if (monitorIndex < 0)
            {
                bounds = monitorBounds[0];
                for (int i = 1; i < monitorBounds.Length; i++) bounds = Rectangle.Union(bounds, monitorBounds[i]);
            }
            else
            {
                bounds = monitorBounds[Math.Clamp(monitorIndex, 0, monitorBounds.Length - 1)];
            }
            Bounds = bounds;

            Logger.Info($"[WebWallpaperForm] Created with bounds: {bounds}");
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 1. Gắn vào WorkerW
            bool attached = _desktopManager.AttachWindow(Handle, Bounds);
            if (!attached)
            {
                Logger.Error("[WebWallpaperForm] Failed to attach to WorkerW!");
                Close();
                return;
            }

            // 2. Khởi tạo WebView2
            try
            {
                _webView = new WebView2
                {
                    Dock = DockStyle.Fill
                };
                Controls.Add(_webView);

                string userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2Data");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(env);

                // Cấu hình tối ưu cho Wallpaper
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

                NavigateTarget(_initialTarget);
                Logger.Info($"[WebWallpaperForm] WebView2 initialized and navigated to: '{_initialTarget}'");

                // 3. Bắt đầu lắng nghe Named Pipe từ Main Process
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ListenToMainProcessPipe(_pipeName, _cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error("[WebWallpaperForm] Exception initializing WebView2", ex);
                MessageBox.Show($"Lỗi nạp WebView2: {ex.Message}", "Lỗi Web Wallpaper", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void NavigateTarget(string target)
        {
            if (_webView?.CoreWebView2 == null) return;

            string url = target;
            if (File.Exists(target))
            {
                url = new Uri(Path.GetFullPath(target)).AbsoluteUri;
            }

            Logger.Info($"[WebWallpaperForm] Navigating CoreWebView2 to: {url}");
            _webView.CoreWebView2.Navigate(url);
        }

        private async Task ListenToMainProcessPipe(string pipeName, CancellationToken ct)
        {
            Logger.Info($"[WebWallpaperForm] Connecting to Named Pipe '{pipeName}'...");

            try
            {
                using NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                await pipeClient.ConnectAsync(5000, ct);

                using StreamReader reader = new StreamReader(pipeClient, Encoding.UTF8);
                Logger.Info("[WebWallpaperForm] Connected to Main Process Named Pipe.");

                while (!ct.IsCancellationRequested && pipeClient.IsConnected)
                {
                    string? line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    Logger.Info($"[WebWallpaperForm] IPC command received: '{line}'");

                    if (line.StartsWith("NAVIGATE "))
                    {
                        string newTarget = line.Substring("NAVIGATE ".Length);
                        BeginInvoke(new Action(() => NavigateTarget(newTarget)));
                    }
                    else if (line == "PAUSE")
                    {
                        BeginInvoke(new Action(() =>
                        {
                            // Tạm dừng animation web / video trong web
                            _webView?.CoreWebView2?.ExecuteScriptAsync("if (window.__pauseWallpaper) window.__pauseWallpaper();");
                            Logger.Info("[WebWallpaperForm] Paused web wallpaper animation.");
                        }));
                    }
                    else if (line == "RESUME")
                    {
                        BeginInvoke(new Action(() =>
                        {
                            _webView?.CoreWebView2?.ExecuteScriptAsync("if (window.__resumeWallpaper) window.__resumeWallpaper();");
                            Logger.Info("[WebWallpaperForm] Resumed web wallpaper animation.");
                        }));
                    }
                    else if (line == "CLOSE")
                    {
                        BeginInvoke(new Action(Close));
                        break;
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Logger.Warn($"[WebWallpaperForm] Named Pipe disconnected or error: {ex.Message}");
                // Khi Main Process ngắt kết nối đột ngột, tự động đóng sub-process
                BeginInvoke(new Action(Close));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Logger.Info("[WebWallpaperForm] Closing and cleaning up...");
            _cts?.Cancel();
            _webView?.Dispose();
            _desktopManager.RestoreDesktop();
            base.OnFormClosing(e);
        }
    }
}
