using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace HardwareAnalysis.Core
{
    /// <summary>
    /// Монитор процессора
    /// </summary>
    public class CpuMonitor : BaseAnalyzer
    {
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter[] _perCpuCounters;

        public override string ComponentName => "CPU";

        public CpuMonitor(MonitoringConfig config = null) : base(config)
        {
            InitializeCounters();
        }

        private void InitializeCounters()
        {
            try
            {
                // Общая загрузка CPU
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // Первый вызов возвращает 0, игнорируем

                // Счётчики для каждого ядра
                int processorCount = Environment.ProcessorCount;
                _perCpuCounters = new PerformanceCounter[processorCount];

                for (int i = 0; i < processorCount; i++)
                {
                    _perCpuCounters[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    _perCpuCounters[i].NextValue();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to initialize CPU performance counters: {ex.Message}");
            }
        }

        public override HardwareMetrics GetCurrentMetrics()
        {
            var metrics = new HardwareMetrics
            {
                ComponentName = ComponentName,
                Timestamp = DateTime.Now
            };

            try
            {
                // Общая загрузка CPU
                if (_cpuCounter != null)
                {
                    metrics.Values["TotalLoad"] = _cpuCounter.NextValue();
                }
                else
                {
                    // Альтернативный метод через Process
                    metrics.Values["TotalLoad"] = GetCpuUsageAlternative();
                }

                // Загрузка каждого ядра
                if (_perCpuCounters != null)
                {
                    for (int i = 0; i < _perCpuCounters.Length; i++)
                    {
                        metrics.Values[$"Core{i}Load"] = _perCpuCounters[i].NextValue();
                    }
                }

                // Дополнительная информация
                metrics.Values["ProcessorCount"] = Environment.ProcessorCount;
                metrics.Values["Is64BitOS"] = Environment.Is64BitOperatingSystem ? 1 : 0;

                // Симулированные данные (в реальной системе читать из MSR или WMI)
                metrics.Values["Temperature"] = GetSimulatedTemperature();
                metrics.Values["Frequency"] = GetSimulatedFrequency();

                // Проверка здоровья
                metrics.IsHealthy = metrics.Values["TotalLoad"] < 95.0 && 
                                    metrics.Values["Temperature"] < 90.0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error getting CPU metrics: {ex.Message}");
                metrics.IsHealthy = false;
            }

            return metrics;
        }

        private double GetCpuUsageAlternative()
        {
            // Простой метод через общее время процесса
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            System.Threading.Thread.Sleep(100);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return cpuUsageTotal * 100;
        }

        private double GetSimulatedTemperature()
        {
            // В реальной системе читать из:
            // Windows: WMI (MSAcpi_ThermalZoneTemperature)
            // Linux: /sys/class/thermal/thermal_zone*/temp
            // Или через C++ MSR reader

            // Для демонстрации: случайное значение 45-75°C
            var rand = new Random();
            return 45 + rand.NextDouble() * 30;
        }

        private double GetSimulatedFrequency()
        {
            // В реальной системе читать из:
            // Windows: WMI (Win32_Processor.CurrentClockSpeed)
            // Linux: /proc/cpuinfo или /sys/devices/system/cpu/cpu*/cpufreq/scaling_cur_freq
            // Или через C++ MSR reader

            // Для демонстрации: 2.4-4.5 GHz
            var rand = new Random();
            return 2400 + rand.NextDouble() * 2100;
        }

        public override void Dispose()
        {
            _cpuCounter?.Dispose();

            if (_perCpuCounters != null)
            {
                foreach (var counter in _perCpuCounters)
                {
                    counter?.Dispose();
                }
            }

            base.Dispose();
        }
    }

    /// <summary>
    /// Монитор оперативной памяти
    /// </summary>
    public class MemoryMonitor : BaseAnalyzer
    {
        private PerformanceCounter _availableMemoryCounter;

        public override string ComponentName => "Memory";

        public MemoryMonitor(MonitoringConfig config = null) : base(config)
        {
            try
            {
                _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to initialize Memory performance counters: {ex.Message}");
            }
        }

        public override HardwareMetrics GetCurrentMetrics()
        {
            var metrics = new HardwareMetrics
            {
                ComponentName = ComponentName,
                Timestamp = DateTime.Now
            };

            try
            {
                // Общая и доступная память
                var memInfo = GC.GetGCMemoryInfo();
                var totalMemoryMB = memInfo.TotalAvailableMemoryBytes / (1024 * 1024);

                double availableMemoryMB = 0;
                if (_availableMemoryCounter != null)
                {
                    availableMemoryMB = _availableMemoryCounter.NextValue();
                }
                else
                {
                    // Альтернатива: используем GC информацию
                    availableMemoryMB = totalMemoryMB * 0.4; // Грубая оценка
                }

                var usedMemoryMB = totalMemoryMB - availableMemoryMB;
                var usagePercent = (usedMemoryMB / totalMemoryMB) * 100;

                metrics.Values["TotalMemoryMB"] = totalMemoryMB;
                metrics.Values["UsedMemoryMB"] = usedMemoryMB;
                metrics.Values["AvailableMemoryMB"] = availableMemoryMB;
                metrics.Values["UsagePercent"] = usagePercent;
                metrics.Values["PageFaults"] = GetPageFaults();

                // Проверка здоровья
                metrics.IsHealthy = usagePercent < 90.0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error getting Memory metrics: {ex.Message}");
                metrics.IsHealthy = false;
            }

            return metrics;
        }

        private double GetPageFaults()
        {
            // В реальной системе:
            // Windows: PerformanceCounter("Memory", "Page Faults/sec")
            // Linux: /proc/vmstat

            var rand = new Random();
            return rand.NextDouble() * 1000;
        }

        public override void Dispose()
        {
            _availableMemoryCounter?.Dispose();
            base.Dispose();
        }
    }

    /// <summary>
    /// Монитор дисков
    /// </summary>
    public class DiskMonitor : BaseAnalyzer
    {
        private readonly string _driveLetter;

        public override string ComponentName => $"Disk ({_driveLetter})";

        public DiskMonitor(string driveLetter = "C:", MonitoringConfig config = null) : base(config)
        {
            _driveLetter = driveLetter;
        }

        public override HardwareMetrics GetCurrentMetrics()
        {
            var metrics = new HardwareMetrics
            {
                ComponentName = ComponentName,
                Timestamp = DateTime.Now
            };

            try
            {
                var drive = new System.IO.DriveInfo(_driveLetter);

                var totalSpaceGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedSpaceGB = totalSpaceGB - freeSpaceGB;
                var usagePercent = (usedSpaceGB / totalSpaceGB) * 100;

                metrics.Values["TotalSpaceGB"] = totalSpaceGB;
                metrics.Values["UsedSpaceGB"] = usedSpaceGB;
                metrics.Values["FreeSpaceGB"] = freeSpaceGB;
                metrics.Values["UsagePercent"] = usagePercent;
                metrics.Values["DriveType"] = (int)drive.DriveType;

                // Симулированные метрики I/O
                metrics.Values["ReadMBps"] = GetSimulatedReadSpeed();
                metrics.Values["WriteMBps"] = GetSimulatedWriteSpeed();

                // Проверка здоровья
                metrics.IsHealthy = usagePercent < 95.0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error getting Disk metrics: {ex.Message}");
                metrics.IsHealthy = false;
            }

            return metrics;
        }

        private double GetSimulatedReadSpeed()
        {
            // В реальной системе:
            // Windows: PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec")
            // Linux: /proc/diskstats

            var rand = new Random();
            return rand.NextDouble() * 500; // 0-500 MB/s
        }

        private double GetSimulatedWriteSpeed()
        {
            var rand = new Random();
            return rand.NextDouble() * 400; // 0-400 MB/s
        }
    }
}
