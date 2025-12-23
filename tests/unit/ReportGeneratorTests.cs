using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using HardwareAnalysisSystem.Core;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Tests.Unit
{
    /// <summary>
    /// Unit тесты для ReportGenerator
    /// </summary>
    public class ReportGeneratorTests
    {
        [Fact]
        public void Constructor_ShouldInitialize()
        {
            // Arrange & Act
            var generator = new ReportGenerator();

            // Assert
            Assert.NotNull(generator);
        }

        [Fact]
        public void AddAnalysisResult_ShouldStoreResult()
        {
            // Arrange
            var generator = new ReportGenerator();
            var result = new AnalysisResult
            {
                AnalyzerName = "TestAnalyzer",
                Score = 85.0,
                Findings = new() { "Test finding" }
            };

            // Act
            generator.AddAnalysisResult(result);
            var report = generator.GenerateTextReportAsync().Result;

            // Assert
            Assert.Contains("TestAnalyzer", report);
            Assert.Contains("85", report);
        }

        [Fact]
        public void AddMetrics_ShouldStoreMetrics()
        {
            // Arrange
            var generator = new ReportGenerator();
            var metrics = new HardwareMetrics
            {
                Timestamp = DateTime.Now,
                ComponentName = "CPU",
                Type = ComponentType.CPU,
                Load = 50.0,
                Temperature = 60.0,
                Frequency = 3500.0
            };

            // Act
            generator.AddMetrics("CPU", metrics);
            var report = generator.GenerateTextReportAsync().Result;

            // Assert
            Assert.Contains("CPU", report);
            Assert.Contains("50", report);
        }

        [Fact]
        public async Task GenerateTextReport_ShouldContainHeader()
        {
            // Arrange
            var generator = new ReportGenerator();

            // Act
            var report = await generator.GenerateTextReportAsync();

            // Assert
            Assert.Contains("ОТЧЁТ ПО АНАЛИЗУ ПРОИЗВОДИТЕЛЬНОСТИ СИСТЕМЫ", report);
            Assert.Contains("=", report);
        }

        [Fact]
        public async Task GenerateTextReport_WithData_ShouldFormatCorrectly()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddMetrics("CPU", new HardwareMetrics
            {
                ComponentName = "CPU",
                Load = 75.5,
                Temperature = 65.2
            });

            generator.AddAnalysisResult(new AnalysisResult
            {
                AnalyzerName = "CPUAnalyzer",
                Score = 90.0,
                Findings = new() { "Normal operation" },
                Recommendations = new() { "Continue monitoring" }
            });

            // Act
            var report = await generator.GenerateTextReportAsync();

            // Assert
            Assert.Contains("CPU", report);
            Assert.Contains("75.5", report);
            Assert.Contains("CPUAnalyzer", report);
            Assert.Contains("90", report);
            Assert.Contains("Normal operation", report);
        }

        [Fact]
        public async Task GenerateJsonReport_ShouldBeValidJson()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddMetrics("CPU", new HardwareMetrics
            {
                ComponentName = "CPU",
                Load = 50.0
            });

            // Act
            var report = await generator.GenerateJsonReportAsync();

            // Assert
            Assert.NotEmpty(report);
            
            // Проверяем, что это валидный JSON
            var parsed = JsonDocument.Parse(report);
            Assert.NotNull(parsed);
        }

        [Fact]
        public async Task GenerateJsonReport_ShouldContainExpectedFields()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddAnalysisResult(new AnalysisResult
            {
                AnalyzerName = "Test",
                Score = 80.0
            });

            // Act
            var report = await generator.GenerateJsonReportAsync();
            var json = JsonDocument.Parse(report);

            // Assert
            Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
            Assert.True(json.RootElement.TryGetProperty("components", out _));
            Assert.True(json.RootElement.TryGetProperty("analysisResults", out _));
            Assert.True(json.RootElement.TryGetProperty("summary", out _));
        }

        [Fact]
        public async Task GenerateCsvReport_ShouldHaveHeader()
        {
            // Arrange
            var generator = new ReportGenerator();

            // Act
            var report = await generator.GenerateCsvReportAsync();

            // Assert
            Assert.StartsWith("Timestamp,Component,Type,Metric,Value", report);
        }

        [Fact]
        public async Task GenerateCsvReport_WithData_ShouldFormatCorrectly()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddMetrics("CPU", new HardwareMetrics
            {
                Timestamp = new DateTime(2024, 12, 23, 10, 30, 0),
                ComponentName = "CPU",
                Type = ComponentType.CPU,
                Values = { ["Load"] = 75.0 }
            });

            // Act
            var report = await generator.GenerateCsvReportAsync();
            var lines = report.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Assert
            Assert.True(lines.Length >= 2); // Header + at least 1 data line
            Assert.Contains("CPU", lines[1]);
            Assert.Contains("75", lines[1]);
        }

        [Fact]
        public async Task SaveReportAsync_ShouldCreateFile()
        {
            // Arrange
            var generator = new ReportGenerator();
            var tempFile = Path.GetTempFileName();
            
            generator.AddMetrics("CPU", new HardwareMetrics
            {
                ComponentName = "CPU",
                Load = 50.0
            });

            try
            {
                // Act
                await generator.SaveReportAsync(tempFile, ReportFormat.Text);

                // Assert
                Assert.True(File.Exists(tempFile));
                var content = await File.ReadAllTextAsync(tempFile);
                Assert.NotEmpty(content);
                Assert.Contains("CPU", content);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Theory]
        [InlineData(ReportFormat.Text)]
        [InlineData(ReportFormat.Json)]
        [InlineData(ReportFormat.Csv)]
        public async Task SaveReportAsync_ShouldSupportAllFormats(ReportFormat format)
        {
            // Arrange
            var generator = new ReportGenerator();
            var tempFile = Path.GetTempFileName();
            
            generator.AddMetrics("CPU", new HardwareMetrics
            {
                ComponentName = "CPU",
                Load = 50.0
            });

            try
            {
                // Act
                await generator.SaveReportAsync(tempFile, format);

                // Assert
                Assert.True(File.Exists(tempFile));
                var content = await File.ReadAllTextAsync(tempFile);
                Assert.NotEmpty(content);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void Clear_ShouldRemoveAllData()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddMetrics("CPU", new HardwareMetrics { Load = 50.0 });
            generator.AddAnalysisResult(new AnalysisResult { Score = 80.0 });

            // Act
            generator.Clear();
            var report = generator.GenerateTextReportAsync().Result;

            // Assert
            Assert.DoesNotContain("CPU", report);
            Assert.DoesNotContain("80", report);
        }

        [Fact]
        public async Task GenerateTextReport_WithBottlenecks_ShouldDisplayThem()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddAnalysisResult(new AnalysisResult
            {
                AnalyzerName = "Test",
                Score = 60.0,
                Bottlenecks = new()
                {
                    new Bottleneck
                    {
                        Component = "CPU",
                        Type = BottleneckType.HighLoad,
                        Severity = Severity.Critical,
                        Description = "CPU overload"
                    }
                }
            });

            // Act
            var report = await generator.GenerateTextReportAsync();

            // Assert
            Assert.Contains("ОБНАРУЖЕННЫЕ УЗКИЕ МЕСТА", report);
            Assert.Contains("CPU", report);
            Assert.Contains("CPU overload", report);
            Assert.Contains("Critical", report);
        }

        [Fact]
        public async Task GenerateTextReport_MultipleComponents_ShouldShowAll()
        {
            // Arrange
            var generator = new ReportGenerator();
            
            generator.AddMetrics("CPU", new HardwareMetrics { ComponentName = "CPU", Load = 50.0 });
            generator.AddMetrics("Memory", new HardwareMetrics { ComponentName = "Memory", Load = 70.0 });
            generator.AddMetrics("Disk", new HardwareMetrics { ComponentName = "Disk", Load = 30.0 });

            // Act
            var report = await generator.GenerateTextReportAsync();

            // Assert
            Assert.Contains("CPU", report);
            Assert.Contains("Memory", report);
            Assert.Contains("Disk", report);
        }
    }
}
