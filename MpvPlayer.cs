using System;
using System.IO;

namespace LiveWallpaper
{
    public class MpvPlayer : IDisposable
    {
        private IntPtr _mpvHandle = IntPtr.Zero;
        private bool _disposed = false;
        private bool _isPaused = false;
        private bool _isMuted = true;
        private int _volume = 0;
        private readonly object _lock = new object();

        public IntPtr Handle => _mpvHandle;
        public bool IsPaused => _isPaused;
        public bool IsMuted => _isMuted;
        public int Volume => _volume;

        public MpvPlayer(IntPtr windowHandle, bool initialMute = true, int initialVolume = 0)
        {
            _isMuted = initialMute;
            _volume = initialVolume;
            Initialize(windowHandle);
        }

        private void Initialize(IntPtr windowHandle)
        {
            Logger.Info($"Creating mpv instance for window 0x{windowHandle.ToInt64():X8}...");

            _mpvHandle = NativeMethods.MpvCreate();
            if (_mpvHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("NativeMethods.MpvCreate() returned null! Hãy đảm bảo libmpv-2.dll x64 đã được nạp.");
            }
            Logger.Info($"mpv instance created: 0x{_mpvHandle.ToInt64():X8}");

            SetOption("wid", windowHandle.ToInt64().ToString());

            // Tối ưu hóa tối đa cho hình nền: GPU decoding, không cache, giải phóng bộ nhớ ngay
            SetOption("vo", "gpu");
            SetOption("hwdec", "auto");
            SetOption("cache", "no");                      // Tắt cache stream vào RAM
            SetOption("demuxer-readahead-secs", "1");      // Chỉ nạp trước tối đa 1 giây video
            SetOption("demuxer-max-bytes", "16MiB");       // Giới hạn buffer demuxer tối đa 16MB
            SetOption("demuxer-max-back-bytes", "0");      // Giải phóng ngay lập tức các frame đã chạy qua
            SetOption("vd-lavc-fast", "yes");              // Giải mã nhanh, tiết kiệm RAM
            SetOption("vd-lavc-threads", "2");             // Giới hạn số luồng giải mã
            SetOption("hwdec-threads", "2");
            SetOption("swapchain-depth", "1");             // Buffer khung hình tối thiểu
            SetOption("audio-buffer", "0.2");              // Buffer âm thanh siêu nhỏ (0.2s)
            SetOption("hr-seek", "no");
            SetOption("osc", "no");                        // Tắt On-Screen-Controller không cần thiết
            SetOption("osd-bar", "no");                    // Tắt OSD bar
            SetOption("load-scripts", "no");               // Không load lua scripts để tiết kiệm RAM
            SetOption("loop-file", "inf");
            SetOption("mute", _isMuted ? "yes" : "no");
            SetOption("volume", _volume.ToString());
            SetOption("keep-open", "yes");
            SetOption("cursor-autohide", "no");

            int initResult = NativeMethods.MpvInitialize(_mpvHandle);
            if (initResult < 0)
            {
                string errMsg = NativeMethods.GetMpvError(initResult);
                Logger.Error($"MpvInitialize failed: {errMsg} (code: {initResult})");
                throw new InvalidOperationException($"MpvInitialize failed: {errMsg}");
            }

            Logger.Info("mpv initialized successfully.");
        }

        public void SetOption(string name, string value)
        {
            if (_mpvHandle == IntPtr.Zero) return;

            int ret = NativeMethods.MpvSetOptionString(_mpvHandle, name, value);
            if (ret < 0)
            {
                Logger.Warn($"Failed to set mpv option '{name}' = '{value}': {NativeMethods.GetMpvError(ret)} (code: {ret})");
            }
            else
            {
                Logger.Info($"mpv option set: '{name}' = '{value}'");
            }
        }

        public void Play(string filePath)
        {
            lock (_lock)
            {
                if (_mpvHandle == IntPtr.Zero)
                {
                    throw new ObjectDisposedException(nameof(MpvPlayer));
                }

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Video file not found at: {filePath}");
                }

                Logger.Info($"Loading video into mpv: '{filePath}'");
                int ret = NativeMethods.MpvCommandUtf8(_mpvHandle, "loadfile", filePath);

                if (ret < 0)
                {
                    string errMsg = NativeMethods.GetMpvError(ret);
                    Logger.Error($"mpv loadfile command failed: {errMsg} (code: {ret})");
                    throw new InvalidOperationException($"mpv loadfile failed: {errMsg}");
                }

                _isPaused = false;
                Logger.Info("mpv loadfile command issued successfully.");
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_mpvHandle != IntPtr.Zero && !_isPaused)
                {
                    int ret = NativeMethods.MpvSetPropertyString(_mpvHandle, "pause", "yes");
                    if (ret >= 0)
                    {
                        _isPaused = true;
                        Logger.Info("mpv playback PAUSED successfully. (CPU & GPU decoding halted)");
                    }
                    else
                    {
                        Logger.Warn($"Failed to pause mpv: {NativeMethods.GetMpvError(ret)}");
                    }
                }
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (_mpvHandle != IntPtr.Zero && _isPaused)
                {
                    int ret = NativeMethods.MpvSetPropertyString(_mpvHandle, "pause", "no");
                    if (ret >= 0)
                    {
                        _isPaused = false;
                        Logger.Info("mpv playback RESUMED successfully.");
                    }
                    else
                    {
                        Logger.Warn($"Failed to resume mpv: {NativeMethods.GetMpvError(ret)}");
                    }
                }
            }
        }

        public void SetMute(bool mute)
        {
            lock (_lock)
            {
                if (_mpvHandle != IntPtr.Zero)
                {
                    int ret = NativeMethods.MpvSetPropertyString(_mpvHandle, "mute", mute ? "yes" : "no");
                    if (ret >= 0)
                    {
                        _isMuted = mute;
                        Logger.Info($"mpv mute state updated: mute={mute}");
                    }
                    else
                    {
                        Logger.Warn($"Failed to update mute state: {NativeMethods.GetMpvError(ret)}");
                    }
                }
            }
        }

        public void SetLoop(bool loopInf)
        {
            lock (_lock)
            {
                if (_mpvHandle != IntPtr.Zero)
                {
                    NativeMethods.MpvSetOptionString(_mpvHandle, "loop-file", loopInf ? "inf" : "no");
                    Logger.Info($"mpv loop-file updated: {(loopInf ? "inf" : "no")}");
                }
            }
        }

        public void SetVolume(int volume)
        {
            lock (_lock)
            {
                _volume = Math.Clamp(volume, 0, 100);
                if (_mpvHandle != IntPtr.Zero)
                {
                    int ret = NativeMethods.MpvSetPropertyString(_mpvHandle, "volume", _volume.ToString());
                    if (ret >= 0)
                    {
                        Logger.Info($"mpv volume updated: {_volume}%");
                    }
                    else
                    {
                        Logger.Warn($"Failed to update volume: {NativeMethods.GetMpvError(ret)}");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                if (_mpvHandle != IntPtr.Zero)
                {
                    Logger.Info("Terminating and destroying mpv instance...");
                    try
                    {
                        NativeMethods.MpvTerminateDestroy(_mpvHandle);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Exception during MpvTerminateDestroy", ex);
                    }
                    _mpvHandle = IntPtr.Zero;
                }
            }
        }
    }
}
