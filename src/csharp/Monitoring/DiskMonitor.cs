using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Monitoring
{
    /// <summary>
    /// Монитор дисковых накопителей
    /// </summary>
    public class DiskMonitor : IHardwareMonitor
    {
        private CancellationTokenSource _cts;
        private Task _monitoringTask;
        private bool _isInitialized;
        private readonly List<DriveInfo> _drives = new();

        public string ComponentName => "Disk";
        public ComponentType Type => ComponentType.Disk;

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
                // Получаем список доступных дисков
                _drives.Clear();
                var allDrives = DriveInfo.GetDrives();
                
                foreach (var drive in allDrives)
                {
                    try
                    {
                        // Проверяем, что диск готов
                        if (drive.IsReady)
                        {
                            _drives.Add(drive);
                        }
                    }
                    catch
                    {
                        // Пропускаем недоступные диски
                    }
                }

                _isInitialized = true;
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not initialize disk monitoring: {ex.Message}");
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Получение текущих метрик дисков
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

            if (!_drives.Any())
            {
                metrics.Values["Status"] = -1; // Нет доступных дисков
                return metrics;
            }

            // Агрегированная статистика по всем дискам
            double totalSpaceGB = 0;
            double usedSpaceGB = 0;
            double freeSpaceGB = 0;

            var driveDetails = new List<string>();

            foreach (var drive in _drives)
            {
                try
                {
                    if (!drive.IsReady)
                        continue;

                    var totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    var usedGB = totalGB - freeGB;

                    totalSpaceGB += totalGB;
                    freeSpaceGB += freeGB;
                    usedSpaceGB += usedGB;

                    // Детали по каждому диску
                    var usagePercent = (usedGB / totalGB) * 100.0;
                    metrics.Values[$"{drive.Name}_TotalGB"] = totalGB;
                    metrics.Values[$"{drive.Name}_UsedGB"] = usedGB;
                    metrics.Values[$"{drive.Name}_FreeGB"] = freeGB;
                    metrics.Values[$"{drive.Name}_UsagePercent"] = usagePercent;
                    metrics.Values[$"{drive.Name}_Type"] = (int)drive.DriveType;
                    metrics.Values[$"{drive.Name}_Format"] = drive.DriveFormat.GetHashCode(); // Для числового представления

                    driveDetails.Add($"{drive.Name} ({drive.DriveType}): {usedGB:F1}/{totalGB:F1} GB ({usagePercent:F1}%)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading drive {drive.Name}: {ex.Message}");
                }
            }

            // Общая статистика
            var overallUsagePercent = totalSpaceGB > 0 ? (usedSpaceGB / totalSpaceGB) * 100.0 : 0;
            
            metrics.Load = overallUsagePercent;
            metrics.Values["TotalSpaceGB"] = totalSpaceGB;
            metrics.Values["UsedSpaceGB"] = usedSpaceGB;
            metrics.Values["FreeSpaceGB"] = freeSpaceGB;
            metrics.Values["UsagePercent"] = overallUsagePercent;
            metrics.Values["DriveCount"] = _drives.Count;
            metrics.Values["Load"] = overallUsagePercent;

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

        /// <summary>
        /// Получить детали по конкретному диску
        /// </summary>
        public DriveDetails GetDriveDetails(string driveName)
        {
            var drive = _drives.FirstOrDefault(d => d.Name == driveName);
            
            if (drive == null || !drive.IsReady)
                return null;

            try
            {
                var totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var usedGB = totalGB - freeGB;

                return new DriveDetails
                {
                    Name = drive.Name,
                    DriveType = drive.DriveType,
                    DriveFormat = drive.DriveFormat,
                    TotalSizeGB = totalGB,
                    UsedSizeGB = usedGB,
                    FreeSizeGB = freeGB,
                    UsagePercent = (usedGB / totalGB) * 100.0,
                    VolumeLabel = drive.VolumeLabel,
                    IsReady = drive.IsReady
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Получить список всех дисков
        /// </summary>
        public List<DriveDetails> GetAllDrives()
        {
            var details = new List<DriveDetails>();
            
            foreach (var drive in _drives)
            {
                var driveDetails = GetDriveDetails(drive.Name);
                if (driveDetails != null)
                {
                    details.Add(driveDetails);
                }
            }

            return details;
        }

        /// <summary>
        /// Проверка на низкое место на диске
        /// </summary>
        public List<string> CheckLowDiskSpace(double thresholdPercent = 90.0)
        {
            var warnings = new List<string>();

            foreach (var drive in _drives)
            {
                try
                {
                    if (!drive.IsReady)
                        continue;

                    var totalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    var usedGB = totalGB - freeGB;
                    var usagePercent = (usedGB / totalGB) * 100.0;

                    if (usagePercent >= thresholdPercent)
                    {
                        warnings.Add($"{drive.Name}: {usagePercent:F1}% заполнен ({freeGB:F1} GB свободно)");
                    }
                }
                catch
                {
                    // Пропускаем
                }
            }

            return warnings;
        }

        /// <summary>
        /// Получить тип накопителя (SSD/HDD)
        /// </summary>
        public string GetDrivePhysicalType(string driveName)
        {
            // В реальной реализации нужно использовать WMI (Windows) или udev (Linux)
            // Для демонстрации возвращаем "Unknown"
            return "Unknown (требуется системный вызов)";
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            StopMonitoringAsync().Wait();
            _cts?.Dispose();
        }
    }

    /// <summary>
    /// Детальная информация о диске
    /// </summary>
    public class DriveDetails
    {
        public string Name { get; set; }
        public DriveType DriveType { get; set; }
        public string DriveFormat { get; set; }
        public double TotalSizeGB { get; set; }
        public double UsedSizeGB { get; set; }
        public double FreeSizeGB { get; set; }
        public double UsagePercent { get; set; }
        public string VolumeLabel { get; set; }
        public bool IsReady { get; set; }
    }
}
