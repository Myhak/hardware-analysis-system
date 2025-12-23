using System;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core;
using HardwareAnalysisSystem.Monitoring;
using HardwareAnalysisSystem.Analysis;

namespace HardwareAnalysisSystem.Demo
{
    /// <summary>
    /// Демонстрация работы системы мониторинга и анализа CPU
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("СИСТЕМА АНАЛИЗА ПРОИЗВОДИТЕЛЬНОСТИ CPU");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Создаём монитор CPU
            var cpuMonitor = new CpuMonitor();
            var cpuAnalyzer = new CpuAnalyzer();
            var reportGenerator = new ReportGenerator();

            try
            {
                // Инициализация
                Console.WriteLine("Инициализация мониторинга...");
                await cpuMonitor.InitializeAsync();
                Console.WriteLine("✓ Инициализация завершена");
                Console.WriteLine();

                // Подписка на события обновления метрик
                cpuMonitor.MetricsUpdated += (sender, e) =>
                {
                    Console.WriteLine($"[{e.Metrics.Timestamp:HH:mm:ss}] " +
                                      $"Загрузка: {e.Metrics.Load:F1}% | " +
                                      $"Температура: {e.Metrics.Temperature?.ToString("F1") ?? "N/A"}°C | " +
                                      $"Частота: {e.Metrics.Frequency?.ToString("F0") ?? "N/A"} MHz");
                    
                    // Добавляем метрики в анализатор
                    cpuAnalyzer.AddMetrics(e.Metrics);
                };

                // Запускаем мониторинг на 10 секунд
                Console.WriteLine("Запуск мониторинга (10 секунд)...");
                Console.WriteLine();
                await cpuMonitor.StartMonitoringAsync(intervalMs: 1000);
                await Task.Delay(10000);

                // Останавливаем мониторинг
                Console.WriteLine();
                Console.WriteLine("Остановка мониторинга...");
                await cpuMonitor.StopMonitoringAsync();
                Console.WriteLine();

                // Выполняем анализ
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("АНАЛИЗ ПРОИЗВОДИТЕЛЬНОСТИ");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();

                var analysisResult = await cpuAnalyzer.AnalyzeAsync();
                
                Console.WriteLine($"Общая оценка: {analysisResult.Score:F1}/100");
                Console.WriteLine();

                if (analysisResult.Findings.Count > 0)
                {
                    Console.WriteLine("РЕЗУЛЬТАТЫ АНАЛИЗА:");
                    foreach (var finding in analysisResult.Findings)
                    {
                        Console.WriteLine($"  • {finding}");
                    }
                    Console.WriteLine();
                }

                if (analysisResult.Recommendations.Count > 0)
                {
                    Console.WriteLine("РЕКОМЕНДАЦИИ:");
                    foreach (var recommendation in analysisResult.Recommendations)
                    {
                        Console.WriteLine($"  → {recommendation}");
                    }
                    Console.WriteLine();
                }

                if (analysisResult.Bottlenecks.Count > 0)
                {
                    Console.WriteLine("ОБНАРУЖЕННЫЕ УЗКИЕ МЕСТА:");
                    foreach (var bottleneck in analysisResult.Bottlenecks)
                    {
                        var icon = bottleneck.Severity switch
                        {
                            Severity.Critical => "🔴",
                            Severity.High => "🟠",
                            Severity.Medium => "🟡",
                            _ => "🟢"
                        };
                        Console.WriteLine($"  {icon} [{bottleneck.Severity}] {bottleneck.Description}");
                    }
                    Console.WriteLine();
                }

                // Дополнительные рекомендации по апгрейду
                var upgradeRecommendations = cpuAnalyzer.GetUpgradeRecommendations();
                if (upgradeRecommendations.Length > 0)
                {
                    Console.WriteLine("РЕКОМЕНДАЦИИ ПО АПГРЕЙДУ:");
                    foreach (var rec in upgradeRecommendations)
                    {
                        Console.WriteLine($"  {rec}");
                    }
                    Console.WriteLine();
                }

                // Генерация отчёта
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("ГЕНЕРАЦИЯ ОТЧЁТА");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();

                reportGenerator.AddAnalysisResult(analysisResult);
                
                // Получаем последние метрики для отчёта
                var latestMetrics = await cpuMonitor.GetMetricsAsync();
                reportGenerator.AddMetrics("CPU", latestMetrics);

                // Сохраняем отчёты
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                
                await reportGenerator.SaveReportAsync($"cpu_report_{timestamp}.txt", ReportFormat.Text);
                Console.WriteLine($"✓ Текстовый отчёт сохранён: cpu_report_{timestamp}.txt");
                
                await reportGenerator.SaveReportAsync($"cpu_report_{timestamp}.json", ReportFormat.Json);
                Console.WriteLine($"✓ JSON отчёт сохранён: cpu_report_{timestamp}.json");
                
                await reportGenerator.SaveReportAsync($"cpu_report_{timestamp}.csv", ReportFormat.Csv);
                Console.WriteLine($"✓ CSV отчёт сохранён: cpu_report_{timestamp}.csv");
                
                Console.WriteLine();

                // Статистика
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("СТАТИСТИКА");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();

                var loadStats = cpuAnalyzer.GetStatistics("Load");
                if (loadStats != null)
                {
                    Console.WriteLine("Загрузка CPU:");
                    Console.WriteLine($"  Среднее:   {loadStats.Mean:F2}%");
                    Console.WriteLine($"  Минимум:   {loadStats.Min:F2}%");
                    Console.WriteLine($"  Максимум:  {loadStats.Max:F2}%");
                    Console.WriteLine($"  Std. Dev:  {loadStats.StdDev:F2}");
                    Console.WriteLine($"  Измерений: {loadStats.Count}");
                }

                Console.WriteLine();
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine("МОНИТОРИНГ ЗАВЕРШЁН");
                Console.WriteLine("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ОШИБКА: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                cpuMonitor.Dispose();
            }
        }
    }
}
