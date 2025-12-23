using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Core
{
    /// <summary>
    /// Генератор отчётов о производительности системы
    /// </summary>
    public class ReportGenerator
    {
        private readonly List<AnalysisResult> _analysisResults = new();
        private readonly Dictionary<string, List<HardwareMetrics>> _metricsData = new();

        /// <summary>
        /// Добавить результат анализа
        /// </summary>
        public void AddAnalysisResult(AnalysisResult result)
        {
            _analysisResults.Add(result);
        }

        /// <summary>
        /// Добавить метрики компонента
        /// </summary>
        public void AddMetrics(string componentName, HardwareMetrics metrics)
        {
            if (!_metricsData.ContainsKey(componentName))
            {
                _metricsData[componentName] = new List<HardwareMetrics>();
            }
            _metricsData[componentName].Add(metrics);
        }

        /// <summary>
        /// Сгенерировать текстовый отчёт
        /// </summary>
        public async Task<string> GenerateTextReportAsync()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("ОТЧЁТ ПО АНАЛИЗУ ПРОИЗВОДИТЕЛЬНОСТИ СИСТЕМЫ");
            sb.AppendLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();

            // Сводка по компонентам
            sb.AppendLine("СВОДКА ПО КОМПОНЕНТАМ");
            sb.AppendLine("-".PadRight(80, '-'));
            foreach (var kvp in _metricsData)
            {
                var latest = kvp.Value.LastOrDefault();
                if (latest != null)
                {
                    sb.AppendLine($"Компонент: {kvp.Key}");
                    sb.AppendLine($"  Загрузка: {latest.Load:F1}%");
                    if (latest.Temperature.HasValue)
                        sb.AppendLine($"  Температура: {latest.Temperature:F1}°C");
                    if (latest.Frequency.HasValue)
                        sb.AppendLine($"  Частота: {latest.Frequency:F0} MHz");
                    sb.AppendLine();
                }
            }

            // Результаты анализа
            sb.AppendLine("РЕЗУЛЬТАТЫ АНАЛИЗА");
            sb.AppendLine("-".PadRight(80, '-'));
            foreach (var result in _analysisResults)
            {
                sb.AppendLine($"Анализатор: {result.AnalyzerName}");
                sb.AppendLine($"Оценка: {result.Score:F1}/100");
                
                if (result.Findings.Any())
                {
                    sb.AppendLine("Обнаружено:");
                    foreach (var finding in result.Findings)
                        sb.AppendLine($"  • {finding}");
                }

                if (result.Recommendations.Any())
                {
                    sb.AppendLine("Рекомендации:");
                    foreach (var rec in result.Recommendations)
                        sb.AppendLine($"  → {rec}");
                }
                sb.AppendLine();
            }

            // Узкие места
            var allBottlenecks = _analysisResults
                .SelectMany(r => r.Bottlenecks)
                .OrderByDescending(b => b.Severity)
                .ToList();

            if (allBottlenecks.Any())
            {
                sb.AppendLine("ОБНАРУЖЕННЫЕ УЗКИЕ МЕСТА");
                sb.AppendLine("-".PadRight(80, '-'));
                foreach (var bottleneck in allBottlenecks)
                {
                    var severityIcon = bottleneck.Severity switch
                    {
                        Severity.Critical => "🔴",
                        Severity.High => "🟠",
                        Severity.Medium => "🟡",
                        _ => "🟢"
                    };
                    sb.AppendLine($"{severityIcon} [{bottleneck.Severity}] {bottleneck.Component}: {bottleneck.Description}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("=".PadRight(80, '='));
            return sb.ToString();
        }

        /// <summary>
        /// Сгенерировать JSON отчёт
        /// </summary>
        public async Task<string> GenerateJsonReportAsync()
        {
            var report = new
            {
                Timestamp = DateTime.Now,
                Components = _metricsData.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        LatestMetrics = kvp.Value.LastOrDefault(),
                        Count = kvp.Value.Count,
                        Statistics = CalculateStatistics(kvp.Value)
                    }
                ),
                AnalysisResults = _analysisResults,
                Summary = GenerateSummary()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            return JsonSerializer.Serialize(report, options);
        }

        /// <summary>
        /// Сгенерировать CSV отчёт
        /// </summary>
        public async Task<string> GenerateCsvReportAsync()
        {
            var sb = new StringBuilder();
            
            // Заголовок
            sb.AppendLine("Timestamp,Component,Type,Metric,Value");

            // Данные
            foreach (var kvp in _metricsData)
            {
                foreach (var metrics in kvp.Value)
                {
                    foreach (var value in metrics.Values)
                    {
                        sb.AppendLine($"{metrics.Timestamp:yyyy-MM-dd HH:mm:ss},{metrics.ComponentName},{metrics.Type},{value.Key},{value.Value}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Сохранить отчёт в файл
        /// </summary>
        public async Task SaveReportAsync(string filePath, ReportFormat format = ReportFormat.Text)
        {
            string content = format switch
            {
                ReportFormat.Json => await GenerateJsonReportAsync(),
                ReportFormat.Csv => await GenerateCsvReportAsync(),
                _ => await GenerateTextReportAsync()
            };

            await File.WriteAllTextAsync(filePath, content);
        }

        /// <summary>
        /// Очистить данные
        /// </summary>
        public void Clear()
        {
            _analysisResults.Clear();
            _metricsData.Clear();
        }

        // Вспомогательные методы
        private object CalculateStatistics(List<HardwareMetrics> metrics)
        {
            if (!metrics.Any())
                return null;

            var loads = metrics.Where(m => m.Load.HasValue).Select(m => m.Load.Value).ToList();
            
            return new
            {
                AvgLoad = loads.Any() ? loads.Average() : 0,
                MaxLoad = loads.Any() ? loads.Max() : 0,
                MinLoad = loads.Any() ? loads.Min() : 0
            };
        }

        private object GenerateSummary()
        {
            var criticalIssues = _analysisResults
                .SelectMany(r => r.Bottlenecks)
                .Count(b => b.Severity == Severity.Critical);

            var avgScore = _analysisResults.Any() 
                ? _analysisResults.Average(r => r.Score) 
                : 0;

            return new
            {
                OverallScore = avgScore,
                CriticalIssues = criticalIssues,
                TotalComponents = _metricsData.Count,
                AnalysisCount = _analysisResults.Count
            };
        }
    }

    /// <summary>
    /// Формат отчёта
    /// </summary>
    public enum ReportFormat
    {
        Text,
        Json,
        Csv,
        Html
    }
}
