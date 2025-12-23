using System;
using System.Linq;
using System.Threading.Tasks;
using HardwareAnalysisSystem.Core;

namespace HardwareAnalysisSystem.Analysis
{
    /// <summary>
    /// Анализатор производительности процессора
    /// </summary>
    public class CpuAnalyzer : BaseAnalyzer
    {
        public override string Name => "CPU Performance Analyzer";

        private const double LOAD_WARNING_THRESHOLD = 80.0;
        private const double LOAD_CRITICAL_THRESHOLD = 95.0;
        private const double TEMP_WARNING_THRESHOLD = 75.0;
        private const double TEMP_CRITICAL_THRESHOLD = 85.0;

        /// <summary>
        /// Анализ производительности CPU
        /// </summary>
        public override async Task<AnalysisResult> AnalyzeAsync()
        {
            var result = new AnalysisResult
            {
                AnalyzerName = Name,
                Timestamp = DateTime.Now
            };

            if (!MetricsHistory.Any())
            {
                result.Score = 0;
                result.Findings.Add("Недостаточно данных для анализа");
                return result;
            }

            // Анализируем загрузку
            var loadAnalysis = AnalyzeLoad();
            result.Findings.AddRange(loadAnalysis.findings);
            result.Recommendations.AddRange(loadAnalysis.recommendations);
            
            // Анализируем температуру
            var tempAnalysis = AnalyzeTemperature();
            result.Findings.AddRange(tempAnalysis.findings);
            result.Recommendations.AddRange(tempAnalysis.recommendations);

            // Анализируем стабильность
            var stabilityAnalysis = AnalyzeStability();
            result.Findings.AddRange(stabilityAnalysis.findings);
            result.Recommendations.AddRange(stabilityAnalysis.recommendations);

            // Определяем узкие места
            result.Bottlenecks.AddRange(DetectBottlenecks());

            // Рассчитываем общую оценку (0-100)
            result.Score = CalculateOverallScore(loadAnalysis.score, tempAnalysis.score, stabilityAnalysis.score);

            return result;
        }

        /// <summary>
        /// Анализ загрузки CPU
        /// </summary>
        private (double score, string[] findings, string[] recommendations) AnalyzeLoad()
        {
            var loadStats = GetStatistics("Load");
            
            if (loadStats == null)
                return (100, Array.Empty<string>(), Array.Empty<string>());

            var findings = new System.Collections.Generic.List<string>();
            var recommendations = new System.Collections.Generic.List<string>();
            double score = 100;

            // Средняя загрузка
            if (loadStats.Mean > LOAD_CRITICAL_THRESHOLD)
            {
                findings.Add($"Критическая загрузка CPU: {loadStats.Mean:F1}% (среднее)");
                recommendations.Add("Необходимо срочно оптимизировать процессы или обновить CPU");
                score -= 40;
            }
            else if (loadStats.Mean > LOAD_WARNING_THRESHOLD)
            {
                findings.Add($"Высокая загрузка CPU: {loadStats.Mean:F1}% (среднее)");
                recommendations.Add("Рассмотрите возможность оптимизации ресурсоёмких процессов");
                score -= 20;
            }
            else if (loadStats.Mean > 60)
            {
                findings.Add($"Умеренная загрузка CPU: {loadStats.Mean:F1}%");
                score -= 10;
            }
            else
            {
                findings.Add($"Нормальная загрузка CPU: {loadStats.Mean:F1}%");
            }

            // Пиковая загрузка
            if (loadStats.Max > 98)
            {
                findings.Add($"Обнаружены пиковые нагрузки до {loadStats.Max:F1}%");
                recommendations.Add("Проверьте фоновые процессы и запланированные задачи");
                score -= 10;
            }

            // Вариативность загрузки
            if (loadStats.StdDev > 25)
            {
                findings.Add("Высокая вариативность загрузки CPU (нестабильная нагрузка)");
                recommendations.Add("Исследуйте причины резких скачков загрузки");
                score -= 5;
            }

            return (Math.Max(0, score), findings.ToArray(), recommendations.ToArray());
        }

        /// <summary>
        /// Анализ температуры CPU
        /// </summary>
        private (double score, string[] findings, string[] recommendations) AnalyzeTemperature()
        {
            var tempStats = GetStatistics("Temperature");
            
            if (tempStats == null || tempStats.Count == 0)
                return (100, new[] { "Данные о температуре недоступны" }, Array.Empty<string>());

            var findings = new System.Collections.Generic.List<string>();
            var recommendations = new System.Collections.Generic.List<string>();
            double score = 100;

            if (tempStats.Mean > TEMP_CRITICAL_THRESHOLD)
            {
                findings.Add($"⚠️ КРИТИЧЕСКАЯ температура: {tempStats.Mean:F1}°C");
                recommendations.Add("СРОЧНО: Проверьте систему охлаждения!");
                recommendations.Add("Очистите радиатор от пыли");
                recommendations.Add("Проверьте термопасту");
                score -= 50;
            }
            else if (tempStats.Mean > TEMP_WARNING_THRESHOLD)
            {
                findings.Add($"Повышенная температура: {tempStats.Mean:F1}°C");
                recommendations.Add("Улучшите вентиляцию корпуса");
                recommendations.Add("Рассмотрите установку более мощного кулера");
                score -= 25;
            }
            else if (tempStats.Mean > 65)
            {
                findings.Add($"Умеренная температура: {tempStats.Mean:F1}°C");
                score -= 10;
            }
            else
            {
                findings.Add($"Нормальная температура: {tempStats.Mean:F1}°C");
            }

            // Проверка термотроттлинга
            if (tempStats.Max > 90)
            {
                findings.Add("⚠️ Возможен термотроттлинг (снижение частоты из-за перегрева)");
                recommendations.Add("Термотроттлинг снижает производительность на 20-40%");
                score -= 15;
            }

            return (Math.Max(0, score), findings.ToArray(), recommendations.ToArray());
        }

        /// <summary>
        /// Анализ стабильности работы
        /// </summary>
        private (double score, string[] findings, string[] recommendations) AnalyzeStability()
        {
            var findings = new System.Collections.Generic.List<string>();
            var recommendations = new System.Collections.Generic.List<string>();
            double score = 100;

            if (MetricsHistory.Count < 10)
            {
                findings.Add("Недостаточно данных для анализа стабильности");
                return (score, findings.ToArray(), recommendations.ToArray());
            }

            // Проверка количества измерений
            var timeSpan = MetricsHistory.Last().Timestamp - MetricsHistory.First().Timestamp;
            findings.Add($"Период мониторинга: {timeSpan.TotalMinutes:F1} минут ({MetricsHistory.Count} измерений)");

            // Проверка на аномалии
            var loadStats = GetStatistics("Load");
            if (loadStats != null)
            {
                var anomalies = MetricsHistory
                    .Where(m => m.Load.HasValue && Math.Abs(m.Load.Value - loadStats.Mean) > 2 * loadStats.StdDev)
                    .Count();

                if (anomalies > MetricsHistory.Count * 0.1)
                {
                    findings.Add($"Обнаружено {anomalies} аномальных значений загрузки");
                    recommendations.Add("Исследуйте причины нестабильной работы");
                    score -= 15;
                }
                else if (anomalies > 0)
                {
                    findings.Add($"Обнаружено {anomalies} выбросов в загрузке (в пределах нормы)");
                }
                else
                {
                    findings.Add("Стабильная работа CPU без аномалий");
                }
            }

            return (Math.Max(0, score), findings.ToArray(), recommendations.ToArray());
        }

        /// <summary>
        /// Расчёт общей оценки
        /// </summary>
        private double CalculateOverallScore(double loadScore, double tempScore, double stabilityScore)
        {
            // Взвешенное среднее: загрузка (40%), температура (35%), стабильность (25%)
            double overall = (loadScore * 0.4) + (tempScore * 0.35) + (stabilityScore * 0.25);
            return Math.Round(overall, 1);
        }

        /// <summary>
        /// Получить рекомендации по апгрейду
        /// </summary>
        public string[] GetUpgradeRecommendations()
        {
            var loadStats = GetStatistics("Load");
            var recommendations = new System.Collections.Generic.List<string>();

            if (loadStats != null && loadStats.Mean > LOAD_WARNING_THRESHOLD)
            {
                var coreCount = Environment.ProcessorCount;
                
                recommendations.Add($"Текущее количество ядер: {coreCount}");
                recommendations.Add("Рекомендации по апгрейду:");
                
                if (coreCount < 8)
                {
                    recommendations.Add("  • Рассмотрите процессор с 8+ ядрами для многозадачности");
                }
                
                recommendations.Add("  • Проверьте совместимость сокета материнской платы");
                recommendations.Add("  • Убедитесь в достаточности системы охлаждения");
                
                if (loadStats.Mean > 90)
                {
                    recommendations.Add("  • ПРИОРИТЕТ: Высокая загрузка требует немедленного апгрейда");
                }
            }

            return recommendations.ToArray();
        }
    }
}
