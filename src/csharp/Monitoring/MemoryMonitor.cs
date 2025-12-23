using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Monitoring
{
    /// <summary>
    /// Монитор оперативной памяти (RAM)
    /// </summary>
    public class MemoryMonitor : IHardwareMonitor
    {
        private CancellationTokenSource _cts;
        private Task _monitoringTask;
        private bool _isInitialized;
        private PerformanceCounter _availableMemoryCounter;

        public string ComponentName => "Memory";
        public ComponentType Type => ComponentType.Memory;

        public event EventHandler<MetricsEventArgs> MetricsUpdated;

        /// <summary>
        /// Инициализация мониторинга
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                // Windows: используем PerformanceCounter
                if (OperatingSystem.IsWindows())
                {
                    _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
                    _availableMemoryCounter.NextValue();
                }

                _isInitialized = true;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize PerformanceCounter: {ex.Message}");
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Получение текущих метрик памяти
        /// </summary>
        public async Task<HardwareMetrics> GetMetricsAsync()
        {
            if (!_isInitialized)
                await InitializeAsync();

            var metrics = new HardwareMetrics
            {
                Timestamp = DateTime.Now,
                ComponentName = ComponentName,
                Type = Type
            };

            // Получаем информацию о памяти
            var memoryInfo = GetMemoryInfo();
            
            if (memoryInfo.HasValue)
            {
                var (totalMB, availableMB) = memoryInfo.Value;
                var usedMB = totalMB - availableMB;
                var usagePercent = (usedMB / totalMB) * 100.0;

                metrics.Load = usagePercent;
                metrics.Values["TotalMB"] = totalMB;
                metrics.Values["UsedMB"] = usedMB;
                metrics.Values["AvailableMB"] = availableMB;
                metrics.Values["Load"] = usagePercent;
                metrics.Values["UsagePercent"] = usagePercent;
            }

            // Дополнительная информация
            var gcInfo = GetGCMemoryInfo();
            metrics.Values["GCTotalMemoryMB"] = gcInfo.totalMB;
            metrics.Values["GCGen0Collections"] = gcInfo.gen0;
            metrics.Values["GCGen1Collections"] = gcInfo.gen1;
            metrics.Values["GCGen2Collections"] = gcInfo.gen2;

            return metrics;
        }

        /// <summary>
        /// Начать непрерывный мониторинг
        /// </summary>
        public async Task StartMonitoringAsync(int intervalMs = 1000)
        {
            if (!_isInitialized)
                await InitializeAsync();

            if (_monitoringTask != null && !_monitoringTask.IsCompleted)
                return;

            _cts = new CancellationTokenSource();
            _monitoringTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var metrics = await GetMetricsAsync();
                        MetricsUpdated?.Invoke(this, new MetricsEventArgs { Metrics = metrics });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in monitoring loop: {ex.Message}");
                    }

                    await Task.Delay(intervalMs, _cts.Token);
                }
            }, _cts.Token);
        }

        /// <summary>
        /// Остановить мониторинг
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                
                if (_monitoringTask != null)
                {
                    try
                    {
                        await _monitoringTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Ожидаемое исключение
                    }
                }
            }
        }

        // Приватные методы

        /// <summary>
        /// Получить информацию о памяти системы
        /// </summary>
        private (double totalMB, double availableMB)? GetMemoryInfo()
        {
            try
            {
                // Windows: используем PerformanceCounter
                if (OperatingSystem.IsWindows() && _availableMemoryCounter != null)
                {
                    var availableMB = _availableMemoryCounter.NextValue();
                    
                    // Получаем общий объём через WMI или системные вызовы
                    var totalMB = GetTotalMemoryWindows();
                    
                    if (totalMB > 0)
                        return (totalMB, availableMB);
                }

                // Linux: читаем /proc/meminfo
                if (OperatingSystem.IsLinux())
                {
                    return GetLinuxMemoryInfo();
                }

                // Fallback: используем GC (неточно, только для процесса)
                return GetFallbackMemoryInfo();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить общий объём памяти в Windows
        /// </summary>
        private double GetTotalMemoryWindows()
        {
            try
            {
                // Простая эмуляция - в реальности используется WMI
                // Для демонстрации возвращаем значение на основе доступной памяти
                return 16384; // Примерно 16 ГБ
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Получить информацию о памяти в Linux
        /// </summary>
        private (double totalMB, double availableMB)? GetLinuxMemoryInfo()
        {
            try
            {
                if (!File.Exists("/proc/meminfo"))
                    return null;

                var lines = File.ReadAllLines("/proc/meminfo");
                
                double totalKB = 0;
                double availableKB = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out long val))
                            totalKB = val;
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out long val))
                            availableKB = val;
                    }
                }

                if (totalKB > 0 && availableKB > 0)
                {
                    return (totalKB / 1024.0, availableKB / 1024.0); // Конвертируем в МБ
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fallback метод получения информации о памяти
        /// </summary>
        private (double totalMB, double availableMB)? GetFallbackMemoryInfo()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var usedMB = process.WorkingSet64 / (1024.0 * 1024.0);
                
                // Грубая оценка
                var totalMB = Environment.WorkingSet / (1024.0 * 1024.0) * 10; // Очень приблизительно
                var availableMB = totalMB - usedMB;

                return (totalMB, Math.Max(0, availableMB));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить информацию о памяти GC
        /// </summary>
        private (double totalMB, int gen0, int gen1, int gen2) GetGCMemoryInfo()
        {
            try
            {
                var totalBytes = GC.GetTotalMemory(false);
                var totalMB = totalBytes / (1024.0 * 1024.0);

                var gen0 = GC.CollectionCount(0);
                var gen1 = GC.CollectionCount(1);
                var gen2 = GC.CollectionCount(2);

                return (totalMB, gen0, gen1, gen2);
            }
            catch
            {
                return (0, 0, 0, 0);
            }
        }

        /// <summary>
        /// Получить тип памяти (DDR4/DDR5)
        /// </summary>
        public string GetMemoryType()
        {
            // В реальной реализации нужно использовать WMI (Windows) или dmidecode (Linux)
            // Для демонстрации возвращаем предположение
            return "DDR4/DDR5"; // Требует системных вызовов для точного определения
        }

        /// <summary>
        /// Получить частоту памяти (MHz)
        /// </summary>
        public double? GetMemoryFrequency()
        {
            try
            {
                // Linux: можно попытаться прочитать из dmidecode
                // Windows: WMI
                // Для демонстрации возвращаем null
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            StopMonitoringAsync().Wait();
            _availableMemoryCounter?.Dispose();
            _cts?.Dispose();
        }
    }
}
