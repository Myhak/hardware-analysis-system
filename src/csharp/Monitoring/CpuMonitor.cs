using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Monitoring
{
    /// <summary>
    /// Монитор процессора (CPU)
    /// </summary>
    public class CpuMonitor : IHardwareMonitor
    {
        private PerformanceCounter _cpuCounter;
        private CancellationTokenSource _cts;
        private Task _monitoringTask;
        private bool _isInitialized;

        public string ComponentName => "CPU";
        public ComponentType Type => ComponentType.CPU;

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
                // Пытаемся использовать PerformanceCounter (Windows)
                if (OperatingSystem.IsWindows())
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuCounter.NextValue(); // Первый вызов всегда возвращает 0
                    await Task.Delay(100); // Небольшая задержка для калибровки
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize PerformanceCounter: {ex.Message}");
                // Продолжаем без PerformanceCounter, будем использовать альтернативные методы
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Получение текущих метрик CPU
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

            // Загрузка CPU
            metrics.Load = await GetCpuLoadAsync();

            // Частота (если доступна)
            metrics.Frequency = GetCpuFrequency();

            // Температура (если доступна)
            metrics.Temperature = GetCpuTemperature();

            // Дополнительные метрики
            metrics.Values["CoreCount"] = Environment.ProcessorCount;
            metrics.Values["Load"] = metrics.Load ?? 0;
            
            if (metrics.Frequency.HasValue)
                metrics.Values["Frequency"] = metrics.Frequency.Value;
            
            if (metrics.Temperature.HasValue)
                metrics.Values["Temperature"] = metrics.Temperature.Value;

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
                        // Ожидаемое исключение при отмене
                    }
                }
            }
        }

        // Приватные методы

        /// <summary>
        /// Получить загрузку CPU
        /// </summary>
        private async Task<double?> GetCpuLoadAsync()
        {
            try
            {
                // Windows: используем PerformanceCounter
                if (OperatingSystem.IsWindows() && _cpuCounter != null)
                {
                    await Task.Delay(100); // Небольшая задержка для точности
                    return _cpuCounter.NextValue();
                }

                // Linux: читаем /proc/stat
                if (OperatingSystem.IsLinux())
                {
                    return await GetLinuxCpuLoadAsync();
                }

                // Fallback: используем Process для текущего процесса (неточно)
                return GetProcessCpuLoad();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить загрузку CPU на Linux
        /// </summary>
        private async Task<double?> GetLinuxCpuLoadAsync()
        {
            try
            {
                if (!File.Exists("/proc/stat"))
                    return null;

                var lines = await File.ReadAllLinesAsync("/proc/stat");
                var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
                
                if (cpuLine == null)
                    return null;

                var values = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Skip(1)
                    .Select(long.Parse)
                    .ToArray();

                if (values.Length < 4)
                    return null;

                long idle = values[3];
                long total = values.Sum();

                // Для точного измерения нужно два замера с интервалом
                // Упрощенная версия: возвращаем процент non-idle времени
                double load = 100.0 * (total - idle) / total;
                return load;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить загрузку CPU текущего процесса (fallback)
        /// </summary>
        private double? GetProcessCpuLoad()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                Thread.Sleep(100);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                return cpuUsageTotal * 100;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить частоту CPU (MHz)
        /// </summary>
        private double? GetCpuFrequency()
        {
            try
            {
                // Linux: читаем /proc/cpuinfo
                if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
                {
                    var lines = File.ReadAllLines("/proc/cpuinfo");
                    var freqLine = lines.FirstOrDefault(l => l.StartsWith("cpu MHz"));
                    
                    if (freqLine != null)
                    {
                        var parts = freqLine.Split(':');
                        if (parts.Length == 2 && double.TryParse(parts[1].Trim(), out double freq))
                        {
                            return freq;
                        }
                    }
                }

                // Windows: через WMI (требуется дополнительная библиотека)
                // Пока возвращаем null
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить температуру CPU (°C)
        /// </summary>
        private double? GetCpuTemperature()
        {
            try
            {
                // Linux: читаем /sys/class/thermal/thermal_zone*/temp
                if (OperatingSystem.IsLinux())
                {
                    var thermalDir = "/sys/class/thermal";
                    if (Directory.Exists(thermalDir))
                    {
                        var zones = Directory.GetDirectories(thermalDir, "thermal_zone*");
                        
                        foreach (var zone in zones)
                        {
                            var tempFile = Path.Combine(zone, "temp");
                            if (File.Exists(tempFile))
                            {
                                var tempStr = File.ReadAllText(tempFile).Trim();
                                if (long.TryParse(tempStr, out long tempMilliC))
                                {
                                    // Температура в милли-градусах
                                    return tempMilliC / 1000.0;
                                }
                            }
                        }
                    }
                }

                // Windows: требуется WMI или специализированная библиотека
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
            _cpuCounter?.Dispose();
            _cts?.Dispose();
        }
    }
}
