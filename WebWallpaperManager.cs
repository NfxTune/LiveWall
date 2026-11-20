using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveWallpaper
{
    public class WebWallpaperManager : IDisposable
    {
        private Process? _webProcess;
        private NamedPipeServerStream? _pipeServer;
        private StreamWriter? _pipeWriter;
        private readonly string _pipeName;
        private bool _disposed = false;

        public bool IsRunning => _webProcess != null && !_webProcess.HasExited;

        public WebWallpaperManager()
        {
            _pipeName = $"LiveWallpaper_WebPipe_{Guid.NewGuid():N}";
        }

        public async Task StartAsync(string initialTarget, int monitorIndex = 0)
        {
            Stop();

            Logger.Info($"[WebManager] Launching Web Host Sub-process for target: '{initialTarget}'...");

            _pipeServer = new NamedPipeServerStream(_pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            string exePath = Application.ExecutablePath;
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--web-host \"{initialTarget}\" \"{_pipeName}\" {monitorIndex}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _webProcess = Process.Start(psi);
            if (_webProcess == null)
            {
                Logger.Error("[WebManager] Failed to launch Web Host sub-process!");
                return;
            }

            Logger.Info($"[WebManager] Web Host process started with PID: {_webProcess.Id}. Waiting for pipe connection...");

            // Chờ sub-process kết nối vào Named Pipe
            var connectTask = _pipeServer.WaitForConnectionAsync();
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(8000));

            if (completedTask == connectTask)
            {
                _pipeWriter = new StreamWriter(_pipeServer, Encoding.UTF8) { AutoFlush = true };
                Logger.Info("[WebManager] Named Pipe connected to Web Host sub-process.");
            }
            else
            {
                Logger.Warn("[WebManager] Timeout waiting for Web Host process to connect to Named Pipe.");
            }
        }

        public void Navigate(string target)
        {
            SendCommand($"NAVIGATE {target}");
        }

        public void Pause()
        {
            SendCommand("PAUSE");
        }

        public void Resume()
        {
            SendCommand("RESUME");
        }

        private void SendCommand(string command)
        {
            if (_pipeWriter != null && _pipeServer != null && _pipeServer.IsConnected)
            {
                try
                {
                    _pipeWriter.WriteLine(command);
                    Logger.Info($"[WebManager] Sent IPC command: '{command}'");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[WebManager] Failed to send command '{command}': {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            if (_pipeWriter != null)
            {
                try
                {
                    SendCommand("CLOSE");
                }
                catch { }
                _pipeWriter.Dispose();
                _pipeWriter = null;
            }

            if (_pipeServer != null)
            {
                try
                {
                    _pipeServer.Dispose();
                }
                catch { }
                _pipeServer = null;
            }

            if (_webProcess != null)
            {
                try
                {
                    if (!_webProcess.HasExited)
                    {
                        Logger.Info($"[WebManager] Terminating Web Host sub-process (PID: {_webProcess.Id})...");
                        if (!_webProcess.WaitForExit(1500))
                        {
                            _webProcess.Kill();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("[WebManager] Exception while stopping Web Host process", ex);
                }
                _webProcess.Dispose();
                _webProcess = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
