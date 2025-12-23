using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HardwareAnalysisSystem.Core.Interfaces
{
    /// <summary>
    /// Базовый интерфейс для мониторинга аппаратных компонентов
    /// </summary>
    public interface IHardwareMonitor
    {
        /// <summary>
        /// Имя компонента (CPU, Memory, Disk, GPU)
        /// </summary>
        string ComponentName { get; }

        /// <summary>
        /// Тип компонента
        /// </summary>
        ComponentType Type { get; }

        /// <summary>
        /// Инициализация мониторинга
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Получение текущих метрик
        /// </summary>
        Task<HardwareMetrics> GetMetricsAsync();

        /// <summary>
        /// Начать непрерывный мониторинг
        /// </summary>
        Task StartMonitoringAsync(int intervalMs = 1000);

        /// <summary>
        /// Остановить мониторинг
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Событие обновления метрик
        /// </summary>
        event EventHandler<MetricsEventArgs> MetricsUpdated;
    }

    /// <summary>
    /// Тип компонента
    /// </summary>
    public enum ComponentType
    {
        CPU,
        Memory,
        Disk,
        GPU,
        Network,
        Motherboard
    }

    /// <summary>
    /// Метрики компонента
    /// </summary>
    public class HardwareMetrics
    {
        public DateTime Timestamp { get; set; }
        public string ComponentName { get; set; }
        public ComponentType Type { get; set; }
        public Dictionary<string, double> Values { get; set; } = new();
        
        // Общие метрики
        public double? Temperature { get; set; }
        public double? Load { get; set; }
        public double? Power { get; set; }
        public double? Frequency { get; set; }
    }

    /// <summary>
    /// Аргументы события обновления метрик
    /// </summary>
    public class MetricsEventArgs : EventArgs
    {
        public HardwareMetrics Metrics { get; set; }
    }
}
