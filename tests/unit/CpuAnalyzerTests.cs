using System;
using System.Threading.Tasks;
using Xunit;
using HardwareAnalysisSystem.Analysis;
using HardwareAnalysisSystem.Core;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Tests.Unit
{
    /// <summary>
    /// Unit тесты для CpuAnalyzer
    /// </summary>
    public class CpuAnalyzerTests
    {
        [Fact]
        public void Constructor_ShouldInitialize()
        {
            // Arrange & Act
            var analyzer = new CpuAnalyzer();

            // Assert
            Assert.NotNull(analyzer);
            Assert.Equal("CPU Performance Analyzer", analyzer.Name);
        }

        [Fact]
        public async Task AnalyzeAsync_WithNoData_ShouldReturnLowScore()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.Score);
            Assert.Contains(result.Findings, f => f.Contains("Недостаточно данных"));
        }

        [Fact]
        public async Task AnalyzeAsync_WithNormalLoad_ShouldReturnHighScore()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            // Добавляем метрики с нормальной загрузкой
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 50.0 }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.True(result.Score >= 80);
            Assert.Contains(result.Findings, f => f.Contains("Нормальная загрузка"));
        }

        [Fact]
        public async Task AnalyzeAsync_WithHighLoad_ShouldDetectIssue()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 85.0 }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.True(result.Score < 90);
            Assert.Contains(result.Findings, f => f.Contains("Высокая загрузка"));
            Assert.NotEmpty(result.Recommendations);
        }

        [Fact]
        public async Task AnalyzeAsync_WithCriticalLoad_ShouldHaveLowScore()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 98.0 }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.True(result.Score < 70);
            Assert.Contains(result.Findings, f => f.Contains("Критическая загрузка"));
            Assert.Contains(result.Recommendations, r => r.Contains("оптимизировать") || r.Contains("обновить"));
        }

        [Fact]
        public async Task AnalyzeAsync_WithNormalTemperature_ShouldBeOk()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = 50.0,
                        ["Temperature"] = 60.0
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Contains(result.Findings, f => f.Contains("Нормальная температура"));
            Assert.True(result.Score >= 80);
        }

        [Fact]
        public async Task AnalyzeAsync_WithHighTemperature_ShouldWarn()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = 50.0,
                        ["Temperature"] = 80.0
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Contains(result.Findings, f => f.Contains("Повышенная температура"));
            Assert.Contains(result.Recommendations, r => r.Contains("вентиляц") || r.Contains("кулер"));
        }

        [Fact]
        public async Task AnalyzeAsync_WithCriticalTemperature_ShouldAlert()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = 50.0,
                        ["Temperature"] = 88.0
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Contains(result.Findings, f => f.Contains("КРИТИЧЕСКАЯ температура"));
            Assert.Contains(result.Recommendations, r => r.Contains("СРОЧНО"));
            Assert.True(result.Score < 60);
        }

        [Fact]
        public async Task AnalyzeAsync_WithThrottling_ShouldDetect()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = 70.0,
                        ["Temperature"] = 92.0 // Выше порога термотроттлинга
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Contains(result.Findings, f => f.Contains("термотроттлинг"));
            Assert.Contains(result.Recommendations, r => r.Contains("производительность"));
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldCalculateWeightedScore()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            // Добавляем данные для расчёта взвешенной оценки
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = 70.0,     // Умеренная загрузка
                        ["Temperature"] = 65.0 // Умеренная температура
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.InRange(result.Score, 0, 100);
            Assert.True(result.Score > 70); // Должно быть довольно хорошо
        }

        [Fact]
        public async Task AnalyzeAsync_WithBottlenecks_ShouldDetectThem()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = 96.0,
                        ["Temperature"] = 86.0
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.NotEmpty(result.Bottlenecks);
            Assert.Contains(result.Bottlenecks, b => b.Type == BottleneckType.HighLoad);
            Assert.Contains(result.Bottlenecks, b => b.Type == BottleneckType.Thermal);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldIncludePeriodInfo()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 15; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    Timestamp = DateTime.Now.AddSeconds(-i),
                    ComponentName = "CPU",
                    Values = { ["Load"] = 50.0 }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Contains(result.Findings, f => f.Contains("Период мониторинга"));
            Assert.Contains(result.Findings, f => f.Contains("15 измерений"));
        }

        [Fact]
        public async Task AnalyzeAsync_WithAnomalies_ShouldDetect()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            // Добавляем нормальные значения
            for (int i = 0; i < 20; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 50.0 }
                });
            }
            
            // Добавляем аномальные значения
            for (int i = 0; i < 5; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 95.0 } // Резкий скачок
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            // Должно быть обнаружено нестабильное поведение
            Assert.Contains(result.Findings, f => 
                f.Contains("аномальных") || f.Contains("нестабильн"));
        }

        [Fact]
        public async Task AnalyzeAsync_WithStableLoad_ShouldReportStability()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 20; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 50.0 + (i % 2) } // Небольшие колебания
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Contains(result.Findings, f => f.Contains("Стабильная работа"));
        }

        [Fact]
        public void GetUpgradeRecommendations_WithLowLoad_ShouldReturnEmpty()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 50.0 }
                });
            }

            // Act
            var recommendations = analyzer.GetUpgradeRecommendations();

            // Assert
            Assert.Empty(recommendations);
        }

        [Fact]
        public void GetUpgradeRecommendations_WithHighLoad_ShouldProvide()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 85.0 }
                });
            }

            // Act
            var recommendations = analyzer.GetUpgradeRecommendations();

            // Assert
            Assert.NotEmpty(recommendations);
            Assert.Contains(recommendations, r => r.Contains("ядр") || r.Contains("процессор"));
        }

        [Fact]
        public void GetUpgradeRecommendations_WithCriticalLoad_ShouldMarkPriority()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 95.0 }
                });
            }

            // Act
            var recommendations = analyzer.GetUpgradeRecommendations();

            // Assert
            Assert.Contains(recommendations, r => r.Contains("ПРИОРИТЕТ"));
        }

        [Theory]
        [InlineData(50.0, 60.0, true)] // Нормальная работа
        [InlineData(85.0, 60.0, false)] // Высокая загрузка
        [InlineData(50.0, 85.0, false)] // Высокая температура
        [InlineData(96.0, 88.0, false)] // Критические параметры
        public async Task AnalyzeAsync_DifferentScenarios_ShouldEvaluateCorrectly(
            double load, double temp, bool shouldBeGood)
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { 
                        ["Load"] = load,
                        ["Temperature"] = temp
                    }
                });
            }

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            if (shouldBeGood)
            {
                Assert.True(result.Score >= 80);
            }
            else
            {
                Assert.True(result.Score < 80);
            }
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldSetAnalyzerName()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            analyzer.AddMetrics(new HardwareMetrics
            {
                Values = { ["Load"] = 50.0 }
            });

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.Equal("CPU Performance Analyzer", result.AnalyzerName);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldSetTimestamp()
        {
            // Arrange
            var analyzer = new CpuAnalyzer();
            analyzer.AddMetrics(new HardwareMetrics
            {
                Values = { ["Load"] = 50.0 }
            });
            var before = DateTime.Now;

            // Act
            var result = await analyzer.AnalyzeAsync();
            var after = DateTime.Now;

            // Assert
            Assert.InRange(result.Timestamp, before, after);
        }
    }
}
