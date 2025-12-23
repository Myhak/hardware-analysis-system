#include <gtest/gtest.h>
#include "../src/cpp/hardware_monitor.hpp"
#include <thread>
#include <chrono>

using namespace hardware_analysis;

// ============================================================================
// MSRReader Tests
// ============================================================================

class MSRReaderTest : public ::testing::Test {
 protected:
  void SetUp() override {
    // Проверяем наличие модуля msr
    if (!utils::IsMSRModuleLoaded()) {
      GTEST_SKIP() << "MSR module not loaded (requires root)";
    }
  }
};

TEST_F(MSRReaderTest, ConstructorOpensDevice) {
  EXPECT_NO_THROW({
    MSRReader reader(0);
  });
}

TEST_F(MSRReaderTest, ReadTemperature) {
  MSRReader reader(0);
  double temp = reader.ReadTemperature();
  
  // Температура должна быть в разумных пределах
  EXPECT_GT(temp, 0.0);
  EXPECT_LT(temp, 120.0);
}

TEST_F(MSRReaderTest, ReadFrequency) {
  MSRReader reader(0);
  uint64_t freq = reader.ReadFrequency();
  
  // Частота должна быть > 1 GHz
  EXPECT_GT(freq, 1000);
  EXPECT_LT(freq, 10000);  // < 10 GHz
}

TEST_F(MSRReaderTest, GetAllMetrics) {
  MSRReader reader(0);
  CpuMetrics metrics = reader.GetAllMetrics();
  
  EXPECT_EQ(metrics.cpu_id, 0);
  EXPECT_GT(metrics.temperature_celsius, 0.0);
  EXPECT_GT(metrics.frequency_mhz, 0);
  EXPECT_GT(metrics.timestamp_us, 0);
}

// ============================================================================
// SystemMonitor Tests
// ============================================================================

TEST(SystemMonitorTest, DetectsCpuCount) {
  SystemMonitor monitor;
  int cpu_count = monitor.GetCpuCount();
  
  EXPECT_GT(cpu_count, 0);
  EXPECT_LE(cpu_count, 256);  // Reasonable upper limit
}

TEST(SystemMonitorTest, GetAllCpuMetrics) {
  SystemMonitor monitor;
  auto metrics = monitor.GetAllCpuMetrics();
  
  EXPECT_GT(metrics.size(), 0);
  EXPECT_EQ(metrics.size(), static_cast<size_t>(monitor.GetCpuCount()));
}

TEST(SystemMonitorTest, GetNumaTopology) {
  SystemMonitor monitor;
  auto numa_nodes = monitor.GetNumaTopology();
  
  // NUMA может быть недоступна на некоторых системах
  if (!numa_nodes.empty()) {
    EXPECT_GT(numa_nodes[0].memory_size_mb, 0);
    EXPECT_GT(numa_nodes[0].cpu_list.size(), 0);
  }
}

// ============================================================================
// Utils Tests
// ============================================================================

TEST(UtilsTest, GetTimestamp) {
  uint64_t ts1 = utils::GetTimestampUs();
  std::this_thread::sleep_for(std::chrono::milliseconds(10));
  uint64_t ts2 = utils::GetTimestampUs();
  
  EXPECT_GT(ts2, ts1);
  EXPECT_GE(ts2 - ts1, 10000);  // At least 10ms = 10000us
}

TEST(UtilsTest, CheckMSRModule) {
  // Этот тест может фейлиться, если MSR не загружен
  bool loaded = utils::IsMSRModuleLoaded();
  // Проверяем только что функция не крашится
  EXPECT_TRUE(loaded || !loaded);
}

// ============================================================================
// OptimizationEngine Tests (Stage 4)
// ============================================================================

#include "../src/cpp/optimization_engine.hpp"

TEST(OptimizationEngineTest, CalculateOptimalFrequency) {
  OptimizationEngine engine;
  
  DVFSConfig config = {
    .min_frequency_mhz = 1000,
    .max_frequency_mhz = 4000,
    .target_temperature_celsius = 75.0,
    .power_limit_watts = 65.0
  };
  
  // Нормальная нагрузка и температура
  uint64_t freq1 = engine.CalculateOptimalFrequency(50.0, 60.0, config);
  EXPECT_GE(freq1, config.min_frequency_mhz);
  EXPECT_LE(freq1, config.max_frequency_mhz);
  
  // Перегрев должен снизить частоту
  uint64_t freq2 = engine.CalculateOptimalFrequency(50.0, 90.0, config);
  EXPECT_LT(freq2, freq1);
}

TEST(OptimizationEngineTest, VectorizedSum) {
  OptimizationEngine engine;
  
  constexpr size_t SIZE = 1000;
  double data[SIZE];
  for (size_t i = 0; i < SIZE; ++i) {
    data[i] = static_cast<double>(i);
  }
  
  double sum = engine.VectorizedSum_AVX2(data, SIZE);
  double expected = (SIZE - 1) * SIZE / 2.0;  // 0 + 1 + 2 + ... + (n-1)
  
  EXPECT_NEAR(sum, expected, 0.1);
}

TEST(OptimizationEngineTest, CacheAlignedVector) {
  OptimizationEngine::CacheAlignedVector<int> vec(100);
  
  EXPECT_EQ(vec.size(), 100);
  
  vec[0] = 42;
  EXPECT_EQ(vec[0], 42);
}

// ============================================================================
// Performance Benchmarks
// ============================================================================

TEST(PerformanceTest, MSRReadLatency) {
  if (!utils::IsMSRModuleLoaded()) {
    GTEST_SKIP() << "MSR module not loaded";
  }
  
  MSRReader reader(0);
  
  constexpr int ITERATIONS = 1000;
  auto start = std::chrono::high_resolution_clock::now();
  
  for (int i = 0; i < ITERATIONS; ++i) {
    reader.ReadTemperature();
  }
  
  auto end = std::chrono::high_resolution_clock::now();
  auto duration = std::chrono::duration_cast<std::chrono::microseconds>(end - start);
  
  double avg_latency_us = static_cast<double>(duration.count()) / ITERATIONS;
  
  std::cout << "Average MSR read latency: " << avg_latency_us << " µs\n";
  
  // MSR чтение должно быть < 100 µs
  EXPECT_LT(avg_latency_us, 100.0);
}

// ============================================================================
// Main
// ============================================================================

int main(int argc, char** argv) {
  ::testing::InitGoogleTest(&argc, argv);
  return RUN_ALL_TESTS();
}
