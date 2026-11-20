using System;
using System.Diagnostics;

namespace LiveWallpaper
{
    public class PerformanceMonitor
    {
        private DateTime _lastCpuTime = DateTime.UtcNow;
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

                return (ramMb, _currentCpuUsage);
            }
            catch
            {
                return (30.0, 0.2);
            }
        }
    }
}
