using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Core
{
    /// <summary>
    /// Базовый класс для анализа производительности
    /// </summary>
    public abstract class BaseAnalyzer
    {
        protected List<HardwareMetrics> MetricsHistory { get; } = new();
        protected int MaxHistorySize { get; set; } = 1000;

        /// <summary>
        /// Имя анализатора
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Добавить метрики в историю
        /// </summary>
        public virtual void AddMetrics(HardwareMetrics metrics)
        {
            MetricsHistory.Add(metrics);
            
            // Ограничиваем размер истории
            if (MetricsHistory.Count > MaxHistorySize)
            {
                MetricsHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Анализ метрик
        /// </summary>
        public abstract Task<AnalysisResult> AnalyzeAsync();

        /// <summary>
        /// Получить статистику
        /// </summary>
        public virtual Statistics GetStatistics(string metricName)
        {
            var values = MetricsHistory
                .Where(m => m.Values.ContainsKey(metricName))
                .Select(m => m.Values[metricName])
                .ToList();

            if (!values.Any())
                return null;

            return new Statistics
            {
                Mean = values.Average(),
                Min = values.Min(),
                Max = values.Max(),
                StdDev = CalculateStdDev(values),
                Count = values.Count
            };
        }

        /// <summary>
        /// Определить узкие места
        /// </summary>
        public virtual List<Bottleneck> DetectBottlenecks()
        {
            var bottlenecks = new List<Bottleneck>();
            
            // Анализируем загрузку
            var loadStats = GetStatistics("Load");
            if (loadStats != null && loadStats.Mean > 90)
            {
                bottlenecks.Add(new Bottleneck
                {
                    Component = Name,
                    Type = BottleneckType.HighLoad,
                    Severity = CalculateSeverity(loadStats.Mean),
                    Description = $"Высокая загрузка: {loadStats.Mean:F1}%"
                });
            }

            // Анализируем температуру
            var tempStats = GetStatistics("Temperature");
            if (tempStats != null && tempStats.Mean > 80)
            {
                bottlenecks.Add(new Bottleneck
                {
                    Component = Name,
                    Type = BottleneckType.Thermal,
                    Severity = CalculateSeverity(tempStats.Mean, 80, 100),
                    Description = $"Высокая температура: {tempStats.Mean:F1}°C"
                });
            }

            return bottlenecks;
        }

        /// <summary>
        /// Очистить историю
        /// </summary>
        public virtual void ClearHistory()
        {
            MetricsHistory.Clear();
        }

        // Вспомогательные методы
        private double CalculateStdDev(List<double> values)
        {
            if (values.Count < 2)
                return 0;

            var mean = values.Average();
            var sumSquares = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquares / (values.Count - 1));
        }

        private Severity CalculateSeverity(double value, double threshold = 90, double critical = 100)
        {
            if (value >= critical)
                return Severity.Critical;
            if (value >= threshold)
                return Severity.High;
            if (value >= threshold * 0.8)
                return Severity.Medium;
            return Severity.Low;
        }
    }

    /// <summary>
    /// Результат анализа
    /// </summary>
    public class AnalysisResult
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string AnalyzerName { get; set; }
        public double Score { get; set; } // 0-100
        public List<string> Findings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public List<Bottleneck> Bottlenecks { get; set; } = new();
    }

    /// <summary>
    /// Статистика метрик
    /// </summary>
    public class Statistics
    {
        public double Mean { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double StdDev { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Узкое место в производительности
    /// </summary>
    public class Bottleneck
    {
        public string Component { get; set; }
        public BottleneckType Type { get; set; }
        public Severity Severity { get; set; }
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }

    public enum BottleneckType
    {
        HighLoad,
        Thermal,
        Memory,
        Disk,
        Network,
        Power
    }

    public enum Severity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
