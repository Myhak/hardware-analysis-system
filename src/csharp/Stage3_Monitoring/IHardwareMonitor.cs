using System;
using System.Collections.Generic;

namespace HardwareAnalysis.Core
{
    /// <summary>
    /// Метрики аппаратного компонента
    /// </summary>
    public class HardwareMetrics
    {
        public string ComponentName { get; set; }
        public Dictionary<string, double> Values { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsHealthy { get; set; }

        public HardwareMetrics()
        {
            Values = new Dictionary<string, double>();
            Timestamp = DateTime.Now;
            IsHealthy = true;
        }
    }

    /// <summary>
    /// Событие алерта при превышении порогов
    /// </summary>
    public class AlertEventArgs : EventArgs
    {
        public string ComponentName { get; set; }
        public string MetricName { get; set; }
        public double CurrentValue { get; set; }
        public double ThresholdValue { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Уровень критичности алерта
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }

    /// <summary>
    /// Базовый интерфейс для мониторинга аппаратных компонентов
    /// </summary>
    public interface IHardwareMonitor
    {
        /// <summary>
        /// Событие при превышении пороговых значений
        /// </summary>
        event EventHandler<AlertEventArgs> OnAlert;

        /// <summary>
        /// Имя компонента (CPU, Memory, Disk, GPU)
        /// </summary>
        string ComponentName { get; }

        /// <summary>
        /// Флаг активности мониторинга
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Запуск мониторинга с заданным интервалом
        /// </summary>
        /// <param name="intervalMs">Интервал обновления в миллисекундах</param>
        void StartMonitoring(int intervalMs);

        /// <summary>
        /// Остановка мониторинга
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Получить текущие метрики
        /// </summary>
        /// <returns>Текущие показатели компонента</returns>
        HardwareMetrics GetCurrentMetrics();

        /// <summary>
        /// Получить историю метрик за период
        /// </summary>
        /// <param name="fromTime">Начало периода</param>
        /// <param name="toTime">Конец периода</param>
        /// <returns>Список метрик</returns>
        List<HardwareMetrics> GetHistoricalMetrics(DateTime fromTime, DateTime toTime);

        /// <summary>
        /// Установка порогов для алертов
        /// </summary>
        /// <param name="metricName">Имя метрики</param>
        /// <param name="threshold">Пороговое значение</param>
        /// <param name="severity">Уровень критичности</param>
        void SetAlertThreshold(string metricName, double threshold, AlertSeverity severity);

        /// <summary>
        /// Очистка истории метрик
        /// </summary>
        void ClearHistory();
    }

    /// <summary>
    /// Конфигурация мониторинга
    /// </summary>
    public class MonitoringConfig
    {
        public int UpdateIntervalMs { get; set; } = 1000;
        public int HistoryRetentionMinutes { get; set; } = 60;
        public bool EnableAlerts { get; set; } = true;
        public bool LogToFile { get; set; } = true;
        public string LogDirectory { get; set; } = "./logs";
        public int MaxHistoryEntries { get; set; } = 3600;
    }
}
