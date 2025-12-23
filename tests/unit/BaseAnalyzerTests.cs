using System;
using System.Threading.Tasks;
using Xunit;
using HardwareAnalysisSystem.Core;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Tests.Unit
{
    /// <summary>
    /// Unit тесты для BaseAnalyzer
    /// </summary>
    public class BaseAnalyzerTests
    {
        // Тестовая реализация BaseAnalyzer
        private class TestAnalyzer : BaseAnalyzer
        {
            public override string Name => "TestAnalyzer";

            public override Task<AnalysisResult> AnalyzeAsync()
            {
                return Task.FromResult(new AnalysisResult
                {
                    AnalyzerName = Name,
                    Score = 85.0,
                    Findings = new() { "Test finding" },
                    Recommendations = new() { "Test recommendation" }
                });
            }
        }

        [Fact]
        public void Constructor_ShouldInitializeEmptyHistory()
        {
            // Arrange & Act
            var analyzer = new TestAnalyzer();

            // Assert
            Assert.NotNull(analyzer);
            Assert.Equal("TestAnalyzer", analyzer.Name);
        }

        [Fact]
        public void AddMetrics_ShouldAddToHistory()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            var metrics = new HardwareMetrics
            {
                Timestamp = DateTime.Now,
                ComponentName = "CPU",
                Type = ComponentType.CPU,
                Load = 50.0,
                Temperature = 60.0
            };

            // Act
            analyzer.AddMetrics(metrics);

            // Assert
            var stats = analyzer.GetStatistics("Load");
            Assert.NotNull(stats);
        }

        [Fact]
        public void AddMetrics_ShouldLimitHistorySize()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            analyzer.MaxHistorySize = 10;

            // Act - добавляем 15 метрик
            for (int i = 0; i < 15; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    Timestamp = DateTime.Now,
                    ComponentName = "CPU",
                    Values = { ["Load"] = i }
                });
            }

            // Assert - должно остаться только 10
            var stats = analyzer.GetStatistics("Load");
            Assert.Equal(10, stats.Count);
        }

        [Fact]
        public void GetStatistics_ShouldCalculateCorrectly()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            var values = new[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            
            foreach (var val in values)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    Values = { ["Load"] = val }
                });
            }

            // Act
            var stats = analyzer.GetStatistics("Load");

            // Assert
            Assert.NotNull(stats);
            Assert.Equal(30.0, stats.Mean, 0.01);
            Assert.Equal(10.0, stats.Min, 0.01);
            Assert.Equal(50.0, stats.Max, 0.01);
            Assert.Equal(5, stats.Count);
            Assert.True(stats.StdDev > 0);
        }

        [Fact]
        public void GetStatistics_WithNoData_ShouldReturnNull()
        {
            // Arrange
            var analyzer = new TestAnalyzer();

            // Act
            var stats = analyzer.GetStatistics("NonExistent");

            // Assert
            Assert.Null(stats);
        }

        [Fact]
        public void DetectBottlenecks_ShouldDetectHighLoad()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            
            // Добавляем метрики с высокой загрузкой
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Load"] = 95.0 }
                });
            }

            // Act
            var bottlenecks = analyzer.DetectBottlenecks();

            // Assert
            Assert.NotEmpty(bottlenecks);
            Assert.Contains(bottlenecks, b => b.Type == BottleneckType.HighLoad);
        }

        [Fact]
        public void DetectBottlenecks_ShouldDetectThermal()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            
            // Добавляем метрики с высокой температурой
            for (int i = 0; i < 10; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    ComponentName = "CPU",
                    Values = { ["Temperature"] = 85.0 }
                });
            }

            // Act
            var bottlenecks = analyzer.DetectBottlenecks();

            // Assert
            Assert.NotEmpty(bottlenecks);
            Assert.Contains(bottlenecks, b => b.Type == BottleneckType.Thermal);
        }

        [Fact]
        public void DetectBottlenecks_WithNormalMetrics_ShouldReturnEmpty()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            
            analyzer.AddMetrics(new HardwareMetrics
            {
                ComponentName = "CPU",
                Values = { 
                    ["Load"] = 50.0,
                    ["Temperature"] = 60.0
                }
            });

            // Act
            var bottlenecks = analyzer.DetectBottlenecks();

            // Assert
            Assert.Empty(bottlenecks);
        }

        [Fact]
        public async Task AnalyzeAsync_ShouldReturnResult()
        {
            // Arrange
            var analyzer = new TestAnalyzer();

            // Act
            var result = await analyzer.AnalyzeAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestAnalyzer", result.AnalyzerName);
            Assert.Equal(85.0, result.Score);
            Assert.NotEmpty(result.Findings);
            Assert.NotEmpty(result.Recommendations);
        }

        [Fact]
        public void ClearHistory_ShouldRemoveAllMetrics()
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            
            for (int i = 0; i < 5; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    Values = { ["Load"] = 50.0 }
                });
            }

            // Act
            analyzer.ClearHistory();

            // Assert
            var stats = analyzer.GetStatistics("Load");
            Assert.Null(stats);
        }

        [Theory]
        [InlineData(95.0, Severity.High)]
        [InlineData(100.0, Severity.Critical)]
        [InlineData(80.0, Severity.Medium)]
        [InlineData(50.0, Severity.Low)]
        public void DetectBottlenecks_ShouldCalculateCorrectSeverity(double load, Severity expectedSeverity)
        {
            // Arrange
            var analyzer = new TestAnalyzer();
            
            for (int i = 0; i < 5; i++)
            {
                analyzer.AddMetrics(new HardwareMetrics
                {
                    Values = { ["Load"] = load }
                });
            }

            // Act
            var bottlenecks = analyzer.DetectBottlenecks();

            // Assert
            if (load >= 90)
            {
                Assert.NotEmpty(bottlenecks);
                Assert.Equal(expectedSeverity, bottlenecks[0].Severity);
            }
        }
    }
}
