using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace HardwareAnalysis.Core
{
    /// <summary>
    /// Базовый класс для всех мониторов с общей функциональностью
    /// </summary>
    public abstract class BaseAnalyzer : IHardwareMonitor, IDisposable
    {
        protected Timer _monitoringTimer;
        protected List<HardwareMetrics> _metricsHistory;
        protected Dictionary<string, (double threshold, AlertSeverity severity)> _alertThresholds;
        protected MonitoringConfig _config;
        protected object _lockObject = new object();

        public event EventHandler<AlertEventArgs> OnAlert;

        public abstract string ComponentName { get; }
        public bool IsMonitoring { get; protected set; }

        protected BaseAnalyzer(MonitoringConfig config = null)
        {
            _config = config ?? new MonitoringConfig();
            _metricsHistory = new List<HardwareMetrics>();
            _alertThresholds = new Dictionary<string, (double, AlertSeverity)>();
            IsMonitoring = false;
        }

        public virtual void StartMonitoring(int intervalMs)
        {
            if (IsMonitoring)
            {
                throw new InvalidOperationException($"{ComponentName} monitoring is already running");
            }

            if (intervalMs <= 0)
            {
                throw new ArgumentException("Interval must be positive", nameof(intervalMs));
            }

            _monitoringTimer = new Timer(intervalMs);
            _monitoringTimer.Elapsed += OnTimerElapsed;
            _monitoringTimer.AutoReset = true;
            _monitoringTimer.Start();

            IsMonitoring = true;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Started monitoring {ComponentName} (interval: {intervalMs}ms)");
        }

        public virtual void StopMonitoring()
        {
            if (!IsMonitoring)
            {
                return;
            }

            _monitoringTimer?.Stop();
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;

            IsMonitoring = false;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Stopped monitoring {ComponentName}");
        }

        public abstract HardwareMetrics GetCurrentMetrics();

        public List<HardwareMetrics> GetHistoricalMetrics(DateTime fromTime, DateTime toTime)
        {
            lock (_lockObject)
            {
                return _metricsHistory
                    .Where(m => m.Timestamp >= fromTime && m.Timestamp <= toTime)
                    .ToList();
            }
        }

        public void SetAlertThreshold(string metricName, double threshold, AlertSeverity severity)
        {
            lock (_lockObject)
            {
                _alertThresholds[metricName] = (threshold, severity);
            }

            Console.WriteLine($"[{ComponentName}] Alert threshold set: {metricName} > {threshold} ({severity})");
        }

        public void ClearHistory()
        {
            lock (_lockObject)
            {
                _metricsHistory.Clear();
            }

            Console.WriteLine($"[{ComponentName}] History cleared");
        }

        protected virtual void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var metrics = GetCurrentMetrics();

                lock (_lockObject)
                {
                    // Добавляем в историю
                    _metricsHistory.Add(metrics);

                    // Ограничиваем размер истории
                    if (_metricsHistory.Count > _config.MaxHistoryEntries)
                    {
                        _metricsHistory.RemoveAt(0);
                    }

                    // Удаляем старые записи по времени
                    var cutoffTime = DateTime.Now.AddMinutes(-_config.HistoryRetentionMinutes);
                    _metricsHistory.RemoveAll(m => m.Timestamp < cutoffTime);
                }

                // Проверяем пороги
                if (_config.EnableAlerts)
                {
                    CheckAlertThresholds(metrics);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{ComponentName}] Error during monitoring: {ex.Message}");
            }
        }

        protected virtual void CheckAlertThresholds(HardwareMetrics metrics)
        {
            lock (_lockObject)
            {
                foreach (var metric in metrics.Values)
                {
                    if (_alertThresholds.TryGetValue(metric.Key, out var threshold))
                    {
                        if (metric.Value > threshold.threshold)
                        {
                            RaiseAlert(new AlertEventArgs
                            {
                                ComponentName = ComponentName,
                                MetricName = metric.Key,
                                CurrentValue = metric.Value,
                                ThresholdValue = threshold.threshold,
                                Severity = threshold.severity,
                                Message = $"{ComponentName}.{metric.Key} exceeded threshold: " +
                                          $"{metric.Value:F2} > {threshold.threshold:F2}",
                                Timestamp = DateTime.Now
                            });
                        }
                    }
                }
            }
        }

        protected void RaiseAlert(AlertEventArgs alert)
        {
            OnAlert?.Invoke(this, alert);

            // Логирование
            var severityColor = alert.Severity switch
            {
                AlertSeverity.Critical => ConsoleColor.Red,
                AlertSeverity.Warning => ConsoleColor.Yellow,
                AlertSeverity.Info => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = severityColor;
            Console.WriteLine($"[ALERT][{alert.Severity}] {alert.Message}");
            Console.ForegroundColor = originalColor;
        }

        /// <summary>
        /// Получение статистики за период
        /// </summary>
        public Dictionary<string, (double min, double max, double avg)> GetStatistics(
            string metricName, 
            DateTime fromTime, 
            DateTime toTime)
        {
            var history = GetHistoricalMetrics(fromTime, toTime);
            var stats = new Dictionary<string, (double, double, double)>();

            if (!history.Any())
            {
                return stats;
            }

            var values = history
                .Where(m => m.Values.ContainsKey(metricName))
                .Select(m => m.Values[metricName])
                .ToList();

            if (values.Any())
            {
                stats[metricName] = (
                    values.Min(),
                    values.Max(),
                    values.Average()
                );
            }

            return stats;
        }

        public virtual void Dispose()
        {
            StopMonitoring();
            _metricsHistory?.Clear();
            _alertThresholds?.Clear();
        }
    }
}
