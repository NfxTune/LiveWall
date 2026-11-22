using System;
using System.Diagnostics;

namespace LiveWallpaper
{
    public class PerformanceMonitor
    {
        private DateTime _lastCpuTime = DateTime.UtcNow;
        private DateTime _lastTrimTime = DateTime.UtcNow;
        private TimeSpan _lastTotalProcessorTime;
        private readonly Process _process;
        private double _currentCpuUsage = 0;

        public PerformanceMonitor()
        {
            _process = Process.GetCurrentProcess();
            _lastTotalProcessorTime = _process.TotalProcessorTime;
        }

        public (double ramMb, double cpuPercent) Sample()
        {
            try
            {
                _process.Refresh();

                // RAM
                double ramMb = _process.WorkingSet64 / (1024.0 * 1024.0);

                // CPU
                DateTime now = DateTime.UtcNow;
                TimeSpan totalProcessorTime = _process.TotalProcessorTime;

                double timeDelta = (now - _lastCpuTime).TotalMilliseconds;
                if (timeDelta > 500)
                {
                    double cpuDelta = (totalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
                    _currentCpuUsage = (cpuDelta / (timeDelta * Environment.ProcessorCount)) * 100.0;
                    _currentCpuUsage = Math.Clamp(_currentCpuUsage, 0.0, 100.0);

                    _lastCpuTime = now;
                    _lastTotalProcessorTime = totalProcessorTime;
                }

                // Tự động thu hồi RAM dư thừa (Trim Working Set) định kỳ mỗi 30 giây hoặc khi RAM > 120MB
                if ((now - _lastTrimTime).TotalSeconds >= 30 || ramMb > 120.0)
                {
                    _lastTrimTime = now;
                    TrimMemory();
                }

                return (ramMb, _currentCpuUsage);
            }
            catch
            {
                return (30.0, 0.2);
            }
        }

        /// <summary>
        /// Yêu cầu Windows dọn sạch bộ nhớ WorkingSet của tiến trình về mức thấp nhất
        /// </summary>
        public void TrimMemory()
        {
            try
            {
                GC.Collect(1, GCCollectionMode.Optimized);
                NativeMethods.EmptyWorkingSet(_process.Handle);
                NativeMethods.SetProcessWorkingSetSize(_process.Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch
            {
                // Bỏ qua nếu không có quyền điều chỉnh working set
            }
        }
    }
}
