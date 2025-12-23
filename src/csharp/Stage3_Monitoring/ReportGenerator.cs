using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace HardwareAnalysis.Core
{
    /// <summary>
    /// Генератор отчётов о производительности
    /// </summary>
    public class ReportGenerator
    {
        private readonly List<IHardwareMonitor> _monitors;

        public ReportGenerator(List<IHardwareMonitor> monitors)
        {
            _monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        }

        /// <summary>
        /// Генерация текстового отчёта
        /// </summary>
        public string GenerateTextReport(DateTime fromTime, DateTime toTime)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine("         HARDWARE MONITORING REPORT");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine($"Period: {fromTime:yyyy-MM-dd HH:mm:ss} - {toTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("═══════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var monitor in _monitors)
            {
                sb.AppendLine($"┌─ {monitor.ComponentName} ".PadRight(55, '─') + "┐");
                
                var history = monitor.GetHistoricalMetrics(fromTime, toTime);
                
                if (!history.Any())
                {
                    sb.AppendLine("│ No data available".PadRight(55) + "│");
                    sb.AppendLine("└".PadRight(55, '─') + "┘");
                    sb.AppendLine();
                    continue;
                }

                var latestMetrics = history.Last();
                sb.AppendLine($"│ Status: {(latestMetrics.IsHealthy ? "✓ Healthy" : "✗ Issues Detected")}".PadRight(55) + "│");
                sb.AppendLine($"│ Samples: {history.Count}".PadRight(55) + "│");
                sb.AppendLine("│".PadRight(55) + "│");

                // Группируем все метрики
                var allMetrics = history.SelectMany(h => h.Values.Keys).Distinct().ToList();

                foreach (var metricName in allMetrics)
                {
                    var values = history
                        .Where(h => h.Values.ContainsKey(metricName))
                        .Select(h => h.Values[metricName])
                        .ToList();

                    if (values.Any())
                    {
                        var min = values.Min();
                        var max = values.Max();
                        var avg = values.Average();
                        var current = values.Last();

                        sb.AppendLine($"│ {metricName}:".PadRight(55) + "│");
                        sb.AppendLine($"│   Current: {current,10:F2}".PadRight(55) + "│");
                        sb.AppendLine($"│   Min:     {min,10:F2}".PadRight(55) + "│");
                        sb.AppendLine($"│   Avg:     {avg,10:F2}".PadRight(55) + "│");
                        sb.AppendLine($"│   Max:     {max,10:F2}".PadRight(55) + "│");
                    }
                }

                sb.AppendLine("└".PadRight(55, '─') + "┘");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Генерация JSON отчёта
        /// </summary>
        public string GenerateJsonReport(DateTime fromTime, DateTime toTime)
        {
            var report = new
            {
                generated_at = DateTime.Now,
                period = new
                {
                    from = fromTime,
                    to = toTime
                },
                components = _monitors.Select(m => new
                {
                    name = m.ComponentName,
                    is_monitoring = m.IsMonitoring,
                    history = m.GetHistoricalMetrics(fromTime, toTime),
                    statistics = CalculateStatistics(m, fromTime, toTime)
                }).ToList()
            };

            return JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        /// <summary>
        /// Генерация CSV отчёта
        /// </summary>
        public string GenerateCsvReport(DateTime fromTime, DateTime toTime)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Component,Metric,Value");

            foreach (var monitor in _monitors)
            {
                var history = monitor.GetHistoricalMetrics(fromTime, toTime);

                foreach (var metrics in history)
                {
                    foreach (var kvp in metrics.Values)
                    {
                        sb.AppendLine($"{metrics.Timestamp:O},{monitor.ComponentName},{kvp.Key},{kvp.Value:F2}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Генерация HTML отчёта с графиками
        /// </summary>
        public string GenerateHtmlReport(DateTime fromTime, DateTime toTime)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head>");
            sb.AppendLine("<title>Hardware Monitoring Report</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
            sb.AppendLine(".header { background: #2c3e50; color: white; padding: 20px; border-radius: 5px; }");
            sb.AppendLine(".component { background: white; margin: 20px 0; padding: 20px; border-radius: 5px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); }");
            sb.AppendLine(".metric { display: inline-block; margin: 10px 20px; }");
            sb.AppendLine(".healthy { color: #27ae60; font-weight: bold; }");
            sb.AppendLine(".unhealthy { color: #e74c3c; font-weight: bold; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 10px; }");
            sb.AppendLine("th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }");
            sb.AppendLine("th { background-color: #34495e; color: white; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head><body>");

            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>Hardware Monitoring Report</h1>");
            sb.AppendLine($"<p>Period: {fromTime:yyyy-MM-dd HH:mm:ss} - {toTime:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine($"<p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("</div>");

            foreach (var monitor in _monitors)
            {
                var history = monitor.GetHistoricalMetrics(fromTime, toTime);
                
                if (!history.Any())
                    continue;

                var latestMetrics = history.Last();
                var healthClass = latestMetrics.IsHealthy ? "healthy" : "unhealthy";

                sb.AppendLine("<div class='component'>");
                sb.AppendLine($"<h2>{monitor.ComponentName} <span class='{healthClass}'>●</span></h2>");
                sb.AppendLine($"<p>Samples: {history.Count}</p>");

                sb.AppendLine("<table>");
                sb.AppendLine("<tr><th>Metric</th><th>Current</th><th>Min</th><th>Avg</th><th>Max</th></tr>");

                var allMetrics = history.SelectMany(h => h.Values.Keys).Distinct();

                foreach (var metricName in allMetrics)
                {
                    var values = history
                        .Where(h => h.Values.ContainsKey(metricName))
                        .Select(h => h.Values[metricName])
                        .ToList();

                    if (values.Any())
                    {
                        sb.AppendLine($"<tr>");
                        sb.AppendLine($"<td><strong>{metricName}</strong></td>");
                        sb.AppendLine($"<td>{values.Last():F2}</td>");
                        sb.AppendLine($"<td>{values.Min():F2}</td>");
                        sb.AppendLine($"<td>{values.Average():F2}</td>");
                        sb.AppendLine($"<td>{values.Max():F2}</td>");
                        sb.AppendLine($"</tr>");
                    }
                }

                sb.AppendLine("</table>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        /// <summary>
        /// Сохранение отчёта в файл
        /// </summary>
        public void SaveReport(string content, string filename)
        {
            File.WriteAllText(filename, content);
            Console.WriteLine($"Report saved to: {filename}");
        }

        private Dictionary<string, object> CalculateStatistics(
            IHardwareMonitor monitor, 
            DateTime fromTime, 
            DateTime toTime)
        {
            var stats = new Dictionary<string, object>();
            var history = monitor.GetHistoricalMetrics(fromTime, toTime);

            if (!history.Any())
                return stats;

            var allMetrics = history.SelectMany(h => h.Values.Keys).Distinct();

            foreach (var metricName in allMetrics)
            {
                var values = history
                    .Where(h => h.Values.ContainsKey(metricName))
                    .Select(h => h.Values[metricName])
                    .ToList();

                if (values.Any())
                {
                    stats[metricName] = new
                    {
                        min = values.Min(),
                        max = values.Max(),
                        avg = values.Average(),
                        stddev = CalculateStandardDeviation(values)
                    };
                }
            }

            return stats;
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1)
                return 0.0;

            var avg = values.Average();
            var sumOfSquaredDiffs = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquaredDiffs / values.Count);
        }
    }
}
