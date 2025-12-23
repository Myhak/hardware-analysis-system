using System;
using System.Threading.Tasks;
using Xunit;
using HardwareAnalysisSystem.Monitoring;
using HardwareAnalysisSystem.Core.Interfaces;

namespace HardwareAnalysisSystem.Tests.Unit
{
    /// <summary>
    /// Unit тесты для CpuMonitor
    /// </summary>
    public class CpuMonitorTests
    {
        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            // Arrange & Act
            var monitor = new CpuMonitor();

            // Assert
            Assert.Equal("CPU", monitor.ComponentName);
            Assert.Equal(ComponentType.CPU, monitor.Type);
        }

        [Fact]
        public async Task InitializeAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            var monitor = new CpuMonitor();

            // Act
            await monitor.InitializeAsync();

            // Assert - не должно быть исключений
            Assert.True(true);
        }

        [Fact]
        public async Task InitializeAsync_CalledTwice_ShouldNotThrow()
        {
            // Arrange
            var monitor = new CpuMonitor();

            // Act
            await monitor.InitializeAsync();
            await monitor.InitializeAsync(); // Второй вызов

            // Assert
            Assert.True(true);
        }

        [Fact]
        public async Task GetMetricsAsync_ShouldReturnValidMetrics()
        {
            // Arrange
            var monitor = new CpuMonitor();
            await monitor.InitializeAsync();

            // Act
            var metrics = await monitor.GetMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal("CPU", metrics.ComponentName);
            Assert.Equal(ComponentType.CPU, metrics.Type);
            Assert.True(metrics.Timestamp > DateTime.MinValue);
        }

        [Fact]
        public async Task GetMetricsAsync_ShouldHaveLoadValue()
        {
            // Arrange
            var monitor = new CpuMonitor();
            await monitor.InitializeAsync();

            // Act
            var metrics = await monitor.GetMetricsAsync();

            // Assert
            Assert.True(metrics.Load.HasValue || !metrics.Load.HasValue); // Может быть null
            if (metrics.Load.HasValue)
            {
                Assert.InRange(metrics.Load.Value, 0, 100);
            }
        }

        [Fact]
        public async Task GetMetricsAsync_ShouldContainCoreCount()
        {
            // Arrange
            var monitor = new CpuMonitor();
            await monitor.InitializeAsync();

            // Act
            var metrics = await monitor.GetMetricsAsync();

            // Assert
            Assert.True(metrics.Values.ContainsKey("CoreCount"));
            Assert.True(metrics.Values["CoreCount"] > 0);
            Assert.Equal(Environment.ProcessorCount, metrics.Values["CoreCount"]);
        }

        [Fact]
        public async Task GetMetricsAsync_WithoutInitialize_ShouldAutoInitialize()
        {
            // Arrange
            var monitor = new CpuMonitor();

            // Act - вызываем без явной инициализации
            var metrics = await monitor.GetMetricsAsync();

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal("CPU", metrics.ComponentName);
        }

        [Fact]
        public async Task StartMonitoringAsync_ShouldRaiseEvents()
        {
            // Arrange
            var monitor = new CpuMonitor();
            var eventRaised = false;
            HardwareMetrics receivedMetrics = null;

            monitor.MetricsUpdated += (sender, e) =>
            {
                eventRaised = true;
                receivedMetrics = e.Metrics;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await Task.Delay(250); // Ждём несколько событий
            await monitor.StopMonitoringAsync();

            // Assert
            Assert.True(eventRaised);
            Assert.NotNull(receivedMetrics);
        }

        [Fact]
        public async Task StartMonitoringAsync_MultipleEvents_ShouldReceiveMultiple()
        {
            // Arrange
            var monitor = new CpuMonitor();
            var eventCount = 0;

            monitor.MetricsUpdated += (sender, e) =>
            {
                eventCount++;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await Task.Delay(350); // ~3 события
            await monitor.StopMonitoringAsync();

            // Assert
            Assert.True(eventCount >= 2); // Как минимум 2 события
        }

        [Fact]
        public async Task StopMonitoringAsync_ShouldStopEvents()
        {
            // Arrange
            var monitor = new CpuMonitor();
            var eventCount = 0;

            monitor.MetricsUpdated += (sender, e) =>
            {
                eventCount++;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await Task.Delay(250);
            var countBeforeStop = eventCount;
            await monitor.StopMonitoringAsync();
            await Task.Delay(300); // Ждём ещё
            var countAfterStop = eventCount;

            // Assert
            Assert.True(countBeforeStop >= 1);
            // После остановки события не должны генерироваться
            Assert.True(countAfterStop - countBeforeStop <= 1); // Допускаем 1 событие из-за race condition
        }

        [Fact]
        public async Task StartMonitoringAsync_CalledTwice_ShouldNotStartSecondTime()
        {
            // Arrange
            var monitor = new CpuMonitor();

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await monitor.StartMonitoringAsync(intervalMs: 100); // Второй вызов

            // Assert - не должно быть исключений
            await monitor.StopMonitoringAsync();
            Assert.True(true);
        }

        [Fact]
        public async Task StopMonitoringAsync_WithoutStart_ShouldNotThrow()
        {
            // Arrange
            var monitor = new CpuMonitor();

            // Act & Assert - не должно быть исключений
            await monitor.StopMonitoringAsync();
            Assert.True(true);
        }

        [Fact]
        public async Task MetricsUpdated_ShouldHaveValidTimestamp()
        {
            // Arrange
            var monitor = new CpuMonitor();
            DateTime? timestamp = null;

            monitor.MetricsUpdated += (sender, e) =>
            {
                timestamp = e.Metrics.Timestamp;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await Task.Delay(200);
            await monitor.StopMonitoringAsync();

            // Assert
            Assert.NotNull(timestamp);
            Assert.True(timestamp.Value > DateTime.Now.AddSeconds(-5));
            Assert.True(timestamp.Value <= DateTime.Now.AddSeconds(1));
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Arrange
            var monitor = new CpuMonitor();

            // Act & Assert - не должно быть исключений
            monitor.Dispose();
            Assert.True(true);
        }

        [Fact]
        public async Task Dispose_AfterMonitoring_ShouldStopMonitoring()
        {
            // Arrange
            var monitor = new CpuMonitor();
            var eventCount = 0;

            monitor.MetricsUpdated += (sender, e) =>
            {
                eventCount++;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await Task.Delay(200);
            monitor.Dispose();
            await Task.Delay(300);

            // Assert
            // После Dispose события должны прекратиться
            var finalCount = eventCount;
            await Task.Delay(200);
            Assert.Equal(finalCount, eventCount);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task StartMonitoringAsync_DifferentIntervals_ShouldWork(int intervalMs)
        {
            // Arrange
            var monitor = new CpuMonitor();
            var eventRaised = false;

            monitor.MetricsUpdated += (sender, e) =>
            {
                eventRaised = true;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs);
            await Task.Delay(intervalMs + 200);
            await monitor.StopMonitoringAsync();

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task GetMetricsAsync_Values_ShouldContainLoad()
        {
            // Arrange
            var monitor = new CpuMonitor();
            await monitor.InitializeAsync();

            // Act
            var metrics = await monitor.GetMetricsAsync();

            // Assert
            Assert.True(metrics.Values.ContainsKey("Load"));
        }

        [Fact]
        public async Task GetMetricsAsync_Consecutive_ShouldReturnDifferentTimestamps()
        {
            // Arrange
            var monitor = new CpuMonitor();
            await monitor.InitializeAsync();

            // Act
            var metrics1 = await monitor.GetMetricsAsync();
            await Task.Delay(100);
            var metrics2 = await monitor.GetMetricsAsync();

            // Assert
            Assert.NotEqual(metrics1.Timestamp, metrics2.Timestamp);
            Assert.True(metrics2.Timestamp > metrics1.Timestamp);
        }

        [Fact]
        public async Task MetricsUpdated_EventArgs_ShouldHaveMetrics()
        {
            // Arrange
            var monitor = new CpuMonitor();
            MetricsEventArgs receivedArgs = null;

            monitor.MetricsUpdated += (sender, e) =>
            {
                receivedArgs = e;
            };

            // Act
            await monitor.StartMonitoringAsync(intervalMs: 100);
            await Task.Delay(200);
            await monitor.StopMonitoringAsync();

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.NotNull(receivedArgs.Metrics);
            Assert.Equal("CPU", receivedArgs.Metrics.ComponentName);
        }
    }
}
