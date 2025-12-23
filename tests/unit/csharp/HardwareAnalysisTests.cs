using Xunit;
using System;
using System.Linq;
using System.Threading;
using HardwareAnalysis.Core;

namespace HardwareAnalysis.Tests
{
    // =========================================================================
    // IHardwareMonitor Interface Tests
    // =========================================================================

    public class CpuMonitorTests
    {
        [Fact]
        public void StartMonitoring_ValidInterval_Succeeds()
        {
            var monitor = new CpuMonitor();
            monitor.StartMonitoring(1000);
            
            Assert.True(monitor.IsMonitoring);
            
            monitor.StopMonitoring();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void StartMonitoring_InvalidInterval_ThrowsException(int interval)
        {
            var monitor = new CpuMonitor();
            
            Assert.Throws<ArgumentException>(() => monitor.StartMonitoring(interval));
        }

        [Fact]
        public void StartMonitoring_AlreadyRunning_ThrowsException()
        {
            var monitor = new CpuMonitor();
            monitor.StartMonitoring(1000);
            
            Assert.Throws<InvalidOperationException>(() => monitor.StartMonitoring(1000));
            
            monitor.StopMonitoring();
        }

        [Fact]
        public void StopMonitoring_WhenRunning_Stops()
        {
            var monitor = new CpuMonitor();
            monitor.StartMonitoring(1000);
            monitor.StopMonitoring();
            
            Assert.False(monitor.IsMonitoring);
        }

        [Fact]
        public void GetCurrentMetrics_ReturnsValidMetrics()
        {
            var monitor = new CpuMonitor();
            var metrics = monitor.GetCurrentMetrics();
            
            Assert.NotNull(metrics);
            Assert.Equal("CPU", metrics.ComponentName);
            Assert.True(metrics.Values.Count > 0);
            Assert.Contains("TotalLoad", metrics.Values.Keys);
        }

        [Fact]
        public void SetAlertThreshold_AddsThreshold()
        {
            var monitor = new CpuMonitor();
            
            // Shouldn't throw
            monitor.SetAlertThreshold("TotalLoad", 80.0, AlertSeverity.Warning);
        }

        [Fact]
        public void OnAlert_TriggersWhenThresholdExceeded()
        {
            var monitor = new CpuMonitor();
            bool alertTriggered = false;
            
            monitor.OnAlert += (sender, e) => {
                alertTriggered = true;
            };
            
            monitor.SetAlertThreshold("TotalLoad", 0.0, AlertSeverity.Critical);
            monitor.StartMonitoring(500);
            
            Thread.Sleep(1500);  // Wait for a few samples
            
            monitor.StopMonitoring();
            
            // Alert should have been triggered (TotalLoad > 0)
            Assert.True(alertTriggered);
        }
    }

    // =========================================================================
    // MemoryMonitor Tests
    // =========================================================================

    public class MemoryMonitorTests
    {
        [Fact]
        public void GetCurrentMetrics_ReturnsMemoryStats()
        {
            var monitor = new MemoryMonitor();
            var metrics = monitor.GetCurrentMetrics();
            
            Assert.NotNull(metrics);
            Assert.Equal("Memory", metrics.ComponentName);
            Assert.Contains("TotalMemoryMB", metrics.Values.Keys);
            Assert.Contains("UsagePercent", metrics.Values.Keys);
            
            Assert.True(metrics.Values["TotalMemoryMB"] > 0);
            Assert.True(metrics.Values["UsagePercent"] >= 0);
            Assert.True(metrics.Values["UsagePercent"] <= 100);
        }
    }

    // =========================================================================
    // DiskMonitor Tests
    // =========================================================================

    public class DiskMonitorTests
    {
        [Fact]
        public void GetCurrentMetrics_ReturnsDiskStats()
        {
            var monitor = new DiskMonitor("C:");
            var metrics = monitor.GetCurrentMetrics();
            
            Assert.NotNull(metrics);
            Assert.Contains("Disk", metrics.ComponentName);
            Assert.Contains("TotalSpaceGB", metrics.Values.Keys);
            Assert.Contains("UsagePercent", metrics.Values.Keys);
            
            Assert.True(metrics.Values["TotalSpaceGB"] > 0);
        }
    }

    // =========================================================================
    // BaseAnalyzer Tests
    // =========================================================================

    public class BaseAnalyzerTests
    {
        private class TestAnalyzer : BaseAnalyzer
        {
            public override string ComponentName => "Test";

            public TestAnalyzer(MonitoringConfig config = null) : base(config) { }

            public override HardwareMetrics GetCurrentMetrics()
            {
                return new HardwareMetrics
                {
                    ComponentName = ComponentName,
                    Timestamp = DateTime.Now,
                    Values = new System.Collections.Generic.Dictionary<string, double>
                    {
                        { "TestMetric", 50.0 }
                    }
                };
            }
        }

        [Fact]
        public void GetHistoricalMetrics_ReturnsCorrectRange()
        {
            var analyzer = new TestAnalyzer();
            analyzer.StartMonitoring(100);
            
            Thread.Sleep(500);  // Collect some samples
            
            var now = DateTime.Now;
            var past = now.AddSeconds(-1);
            var history = analyzer.GetHistoricalMetrics(past, now);
            
            analyzer.StopMonitoring();
            
            Assert.True(history.Count > 0);
            Assert.All(history, m => {
                Assert.True(m.Timestamp >= past);
                Assert.True(m.Timestamp <= now);
            });
        }

        [Fact]
        public void ClearHistory_RemovesAllMetrics()
        {
            var analyzer = new TestAnalyzer();
            analyzer.StartMonitoring(100);
            
            Thread.Sleep(500);
            
            var before = analyzer.GetHistoricalMetrics(
                DateTime.Now.AddMinutes(-1), 
                DateTime.Now
            ).Count;
            
            Assert.True(before > 0);
            
            analyzer.ClearHistory();
            
            var after = analyzer.GetHistoricalMetrics(
                DateTime.Now.AddMinutes(-1), 
                DateTime.Now
            ).Count;
            
            analyzer.StopMonitoring();
            
            Assert.Equal(0, after);
        }

        [Fact]
        public void GetStatistics_CalculatesCorrectValues()
        {
            var analyzer = new TestAnalyzer();
            analyzer.StartMonitoring(100);
            
            Thread.Sleep(500);
            
            var stats = analyzer.GetStatistics(
                "TestMetric",
                DateTime.Now.AddSeconds(-1),
                DateTime.Now
            );
            
            analyzer.StopMonitoring();
            
            Assert.Contains("TestMetric", stats.Keys);
            
            var (min, max, avg) = stats["TestMetric"];
            Assert.Equal(50.0, min);
            Assert.Equal(50.0, max);
            Assert.Equal(50.0, avg);
        }
    }

    // =========================================================================
    // ReportGenerator Tests
    // =========================================================================

    public class ReportGeneratorTests
    {
        [Fact]
        public void GenerateTextReport_CreatesReport()
        {
            var monitors = new System.Collections.Generic.List<IHardwareMonitor>
            {
                new CpuMonitor()
            };

            var generator = new ReportGenerator(monitors);
            
            var report = generator.GenerateTextReport(
                DateTime.Now.AddMinutes(-5),
                DateTime.Now
            );
            
            Assert.NotNull(report);
            Assert.Contains("HARDWARE MONITORING REPORT", report);
            Assert.Contains("CPU", report);
        }

        [Fact]
        public void GenerateJsonReport_CreatesValidJson()
        {
            var monitors = new System.Collections.Generic.List<IHardwareMonitor>
            {
                new CpuMonitor()
            };

            var generator = new ReportGenerator(monitors);
            
            var json = generator.GenerateJsonReport(
                DateTime.Now.AddMinutes(-5),
                DateTime.Now
            );
            
            Assert.NotNull(json);
            Assert.Contains("\"generated_at\"", json);
            Assert.Contains("\"components\"", json);
        }

        [Fact]
        public void GenerateCsvReport_CreatesValidCsv()
        {
            var monitors = new System.Collections.Generic.List<IHardwareMonitor>
            {
                new CpuMonitor()
            };

            var generator = new ReportGenerator(monitors);
            
            var csv = generator.GenerateCsvReport(
                DateTime.Now.AddMinutes(-5),
                DateTime.Now
            );
            
            Assert.NotNull(csv);
            Assert.Contains("Timestamp,Component,Metric,Value", csv);
        }
    }

    // =========================================================================
    // Stage1 Memory Simulator Tests
    // =========================================================================

    public class MemorySimulatorTests
    {
        [Fact]
        public void MeasureSequentialAccess_ReturnsValidResult()
        {
            var sim = new HardwareAnalysis.Stage1.AdvancedMemorySimulator();
            
            int[] array = new int[1024 * 1024];  // 1M elements
            for (int i = 0; i < array.Length; i++)
                array[i] = i;
            
            var result = sim.MeasureSequentialAccess(array);
            
            Assert.NotNull(result);
            Assert.Equal("Sequential Access", result.TestName);
            Assert.True(result.BandwidthGBs > 0);
            Assert.True(result.TimeMs > 0);
        }

        [Fact]
        public void MeasureRandomAccess_SlowerThanSequential()
        {
            var sim = new HardwareAnalysis.Stage1.AdvancedMemorySimulator();
            
            int[] array = new int[1024 * 1024];
            for (int i = 0; i < array.Length; i++)
                array[i] = i;
            
            var sequential = sim.MeasureSequentialAccess(array);
            var random = sim.MeasureRandomAccess(array);
            
            // Random access should be slower
            Assert.True(random.BandwidthGBs < sequential.BandwidthGBs);
        }

        [Theory]
        [InlineData("DDR4-3200", 1, 25.6)]
        [InlineData("DDR5-4800", 1, 38.4)]
        [InlineData("DDR5-6000", 2, 96.0)]
        public void CalculateTheoreticalBandwidth_ReturnsCorrectValue(
            string memType, int channels, double expected)
        {
            var sim = new HardwareAnalysis.Stage1.AdvancedMemorySimulator();
            
            double bandwidth = sim.CalculateTheoreticalBandwidth(memType, channels);
            
            Assert.Equal(expected, bandwidth, precision: 1);
        }
    }

    // =========================================================================
    // Performance/Stress Tests
    // =========================================================================

    public class PerformanceTests
    {
        [Fact]
        public void MonitoringPerformance_LowOverhead()
        {
            var monitor = new CpuMonitor();
            
            var startMem = GC.GetTotalMemory(forceFullCollection: true);
            
            monitor.StartMonitoring(100);
            Thread.Sleep(2000);  // Monitor for 2 seconds
            monitor.StopMonitoring();
            
            var endMem = GC.GetTotalMemory(forceFullCollection: false);
            var memoryIncreaseMB = (endMem - startMem) / (1024.0 * 1024.0);
            
            // Memory overhead should be < 10 MB
            Assert.True(memoryIncreaseMB < 10.0);
        }
    }
}
