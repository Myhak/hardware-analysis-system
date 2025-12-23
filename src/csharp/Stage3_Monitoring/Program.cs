using System;
using System.Collections.Generic;
using System.Threading;
using HardwareAnalysis.Core;

namespace HardwareAnalysis.Stage3
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("   Hardware Analysis System - Real-time Monitoring");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine();

            // Конфигурация мониторинга
            var config = new MonitoringConfig
            {
                UpdateIntervalMs = 1000,  // 1 секунда
                HistoryRetentionMinutes = 60,
                EnableAlerts = true,
                LogToFile = true,
                MaxHistoryEntries = 3600
            };

            // Создание мониторов
            var monitors = new List<IHardwareMonitor>
            {
                new CpuMonitor(config),
                new MemoryMonitor(config),
                new DiskMonitor("C:", config)
            };

            // Настройка алертов
            Console.WriteLine("Setting up alert thresholds...");
            monitors[0].SetAlertThreshold("TotalLoad", 80.0, AlertSeverity.Warning);
            monitors[0].SetAlertThreshold("TotalLoad", 95.0, AlertSeverity.Critical);
            monitors[0].SetAlertThreshold("Temperature", 85.0, AlertSeverity.Warning);
            monitors[0].SetAlertThreshold("Temperature", 95.0, AlertSeverity.Critical);

            monitors[1].SetAlertThreshold("UsagePercent", 85.0, AlertSeverity.Warning);
            monitors[1].SetAlertThreshold("UsagePercent", 95.0, AlertSeverity.Critical);

            monitors[2].SetAlertThreshold("UsagePercent", 90.0, AlertSeverity.Warning);
            monitors[2].SetAlertThreshold("UsagePercent", 95.0, AlertSeverity.Critical);

            // Подписка на события алертов
            foreach (var monitor in monitors)
            {
                monitor.OnAlert += OnAlertReceived;
            }

            Console.WriteLine("Alert thresholds configured.\n");

            // Запуск мониторинга
            Console.WriteLine("Starting monitoring...");
            foreach (var monitor in monitors)
            {
                monitor.StartMonitoring(config.UpdateIntervalMs);
            }

            Console.WriteLine();
            Console.WriteLine("Monitoring active. Press any key to view live data...");
            Console.WriteLine("Press 'Q' to stop and generate report.");
            Console.WriteLine();

            // Интерактивный режим
            bool running = true;
            DateTime startTime = DateTime.Now;

            while (running)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);

                    if (key.Key == ConsoleKey.Q)
                    {
                        running = false;
                    }
                    else
                    {
                        DisplayLiveData(monitors);
                    }
                }

                Thread.Sleep(500);
            }

            // Остановка мониторинга
            Console.WriteLine("\n\nStopping monitoring...");
            foreach (var monitor in monitors)
            {
                monitor.StopMonitoring();
            }

            // Генерация отчётов
            DateTime endTime = DateTime.Now;
            GenerateReports(monitors, startTime, endTime);

            // Очистка ресурсов
            foreach (var monitor in monitors)
            {
                if (monitor is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void OnAlertReceived(object sender, AlertEventArgs e)
        {
            // Обработчик уже логирует в консоль через BaseAnalyzer.RaiseAlert
            // Здесь можно добавить дополнительную логику, например:
            // - Отправка email
            // - Запись в БД
            // - Интеграция с системой мониторинга (Prometheus, Grafana)
        }

        static void DisplayLiveData(List<IHardwareMonitor> monitors)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine("           LIVE MONITORING DATA");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine($"Time: {DateTime.Now:HH:mm:ss}");
            Console.WriteLine();

            foreach (var monitor in monitors)
            {
                var metrics = monitor.GetCurrentMetrics();
                
                Console.WriteLine($"┌─ {monitor.ComponentName} ".PadRight(55, '─') + "┐");
                Console.WriteLine($"│ Status: {(metrics.IsHealthy ? "✓ Healthy" : "✗ Issues")}".PadRight(55) + "│");
                Console.WriteLine($"│ Timestamp: {metrics.Timestamp:HH:mm:ss}".PadRight(55) + "│");
                Console.WriteLine("│".PadRight(55) + "│");

                foreach (var kvp in metrics.Values)
                {
                    string line = $"│ {kvp.Key}: {kvp.Value:F2}";
                    Console.WriteLine(line.PadRight(55) + "│");
                }

                Console.WriteLine("└".PadRight(55, '─') + "┘");
                Console.WriteLine();
            }

            Console.WriteLine("Press any key to refresh, 'Q' to quit...");
        }

        static void GenerateReports(List<IHardwareMonitor> monitors, DateTime startTime, DateTime endTime)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════");
            Console.WriteLine("           GENERATING REPORTS");
            Console.WriteLine("═══════════════════════════════════════════════════════\n");

            var reportGen = new ReportGenerator(monitors);

            // Текстовый отчёт
            Console.WriteLine("Generating text report...");
            var textReport = reportGen.GenerateTextReport(startTime, endTime);
            Console.WriteLine(textReport);
            reportGen.SaveReport(textReport, "monitoring_report.txt");

            // JSON отчёт
            Console.WriteLine("\nGenerating JSON report...");
            var jsonReport = reportGen.GenerateJsonReport(startTime, endTime);
            reportGen.SaveReport(jsonReport, "monitoring_report.json");

            // CSV отчёт
            Console.WriteLine("Generating CSV report...");
            var csvReport = reportGen.GenerateCsvReport(startTime, endTime);
            reportGen.SaveReport(csvReport, "monitoring_report.csv");

            // HTML отчёт
            Console.WriteLine("Generating HTML report...");
            var htmlReport = reportGen.GenerateHtmlReport(startTime, endTime);
            reportGen.SaveReport(htmlReport, "monitoring_report.html");

            Console.WriteLine("\n═══════════════════════════════════════════════════════");
            Console.WriteLine("All reports generated successfully!");
            Console.WriteLine("═══════════════════════════════════════════════════════");

            // Статистика
            Console.WriteLine("\n=== Session Statistics ===");
            Console.WriteLine($"Duration: {(endTime - startTime).TotalMinutes:F1} minutes");
            Console.WriteLine($"Start: {startTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"End: {endTime:yyyy-MM-dd HH:mm:ss}");

            foreach (var monitor in monitors)
            {
                var history = monitor.GetHistoricalMetrics(startTime, endTime);
                Console.WriteLine($"{monitor.ComponentName}: {history.Count} samples collected");
            }
        }
    }
}
