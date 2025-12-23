#include "optimization_engine.hpp"
#include <cpuid.h>
#include <numa.h>
#include <sched.h>
#include <cmath>
#include <fstream>
#include <iostream>
#include <algorithm>

namespace hardware_analysis {

OptimizationEngine::OptimizationEngine() {
  avx2_supported_ = CheckAVX2Support();
  avx512_supported_ = CheckAVX512Support();
  
  std::cout << "Optimization Engine initialized\n";
  std::cout << "  AVX2: " << (avx2_supported_ ? "supported" : "not supported") << "\n";
  std::cout << "  AVX-512: " << (avx512_supported_ ? "supported" : "not supported") << "\n";
}

// ============================================================================
// DVFS оптимизация
// ============================================================================

uint64_t OptimizationEngine::CalculateOptimalFrequency(
    double current_load_percent,
    double current_temp_celsius,
    const DVFSConfig& config) {
  
  // Базовая частота на основе загрузки
  uint64_t target_freq = config.min_frequency_mhz + 
      static_cast<uint64_t>((config.max_frequency_mhz - config.min_frequency_mhz) * 
                             (current_load_percent / 100.0));
  
  // Снижение частоты при перегреве (thermal throttling)
  if (current_temp_celsius > config.target_temperature_celsius) {
    double temp_ratio = config.target_temperature_celsius / current_temp_celsius;
    target_freq = static_cast<uint64_t>(target_freq * temp_ratio);
  }
  
  // Ограничение диапазона
  target_freq = std::max(config.min_frequency_mhz, 
                         std::min(config.max_frequency_mhz, target_freq));
  
  return target_freq;
}

bool OptimizationEngine::SetCpuFrequency(int cpu_id, uint64_t frequency_mhz) {
  // Запись в sysfs для управления частотой (требует root)
  std::string path = "/sys/devices/system/cpu/cpu" + std::to_string(cpu_id) + 
                     "/cpufreq/scaling_setspeed";
  
  std::ofstream file(path);
  if (!file.is_open()) {
    std::cerr << "Failed to open " << path << " (root required)\n";
    return false;
  }
  
  file << frequency_mhz * 1000;  // В kHz
  file.close();
  
  return true;
}

// ============================================================================
// NUMA оптимизация
// ============================================================================

bool OptimizationEngine::BindProcessToNumaNode(pid_t pid, int numa_node) {
  if (numa_available() == -1) {
    std::cerr << "NUMA not available\n";
    return false;
  }
  
  struct bitmask* nodemask = numa_allocate_nodemask();
  numa_bitmask_setbit(nodemask, numa_node);
  
  int ret = numa_run_on_node_mask(nodemask);
  numa_free_nodemask(nodemask);
  
  if (ret != 0) {
    std::cerr << "Failed to bind to NUMA node " << numa_node << "\n";
    return false;
  }
  
  std::cout << "Process bound to NUMA node " << numa_node << "\n";
  return true;
}

int OptimizationEngine::FindBestNumaNode() {
  if (numa_available() == -1) {
    return 0;  // NUMA недоступна
  }
  
  int num_nodes = numa_num_configured_nodes();
  int best_node = 0;
  long best_free_memory = 0;
  
  for (int node = 0; node < num_nodes; ++node) {
    long free_mem = numa_node_size64(node, nullptr);
    if (free_mem > best_free_memory) {
      best_free_memory = free_mem;
      best_node = node;
    }
  }
  
  return best_node;
}

// ============================================================================
// Векторизация (AVX2)
// ============================================================================

double OptimizationEngine::VectorizedSum_AVX2(const double* array, size_t size) {
  if (!avx2_supported_) {
    // Fallback: обычное суммирование
    double sum = 0.0;
    for (size_t i = 0; i < size; ++i) {
      sum += array[i];
    }
    return sum;
  }
  
  __m256d sum_vec = _mm256_setzero_pd();
  
  // Обработка по 4 элемента (256 бит / 64 бит на double)
  size_t i = 0;
  for (; i + 3 < size; i += 4) {
    __m256d data = _mm256_loadu_pd(&array[i]);
    sum_vec = _mm256_add_pd(sum_vec, data);
  }
  
  // Горизонтальное суммирование вектора
  double sum_array[4];
  _mm256_storeu_pd(sum_array, sum_vec);
  double sum = sum_array[0] + sum_array[1] + sum_array[2] + sum_array[3];
  
  // Обработка остатка
  for (; i < size; ++i) {
    sum += array[i];
  }
  
  return sum;
}

void OptimizationEngine::MatrixMultiply_AVX2(
    const double* A, const double* B, double* C,
    size_t M, size_t K, size_t N) {
  
  if (!avx2_supported_) {
    // Наивная реализация без векторизации
    for (size_t i = 0; i < M; ++i) {
      for (size_t j = 0; j < N; ++j) {
        double sum = 0.0;
        for (size_t k = 0; k < K; ++k) {
          sum += A[i * K + k] * B[k * N + j];
        }
        C[i * N + j] = sum;
      }
    }
    return;
  }
  
  // Векторизованная версия (упрощённая)
  for (size_t i = 0; i < M; ++i) {
    for (size_t j = 0; j < N; j += 4) {
      __m256d sum_vec = _mm256_setzero_pd();
      
      for (size_t k = 0; k < K; ++k) {
        __m256d a_vec = _mm256_set1_pd(A[i * K + k]);
        __m256d b_vec = _mm256_loadu_pd(&B[k * N + j]);
        sum_vec = _mm256_add_pd(sum_vec, _mm256_mul_pd(a_vec, b_vec));
      }
      
      _mm256_storeu_pd(&C[i * N + j], sum_vec);
    }
  }
}

// ============================================================================
// Prefetching
// ============================================================================

void OptimizationEngine::Prefetch(const void* ptr, int hint) {
  // __builtin_prefetch доступен в GCC/Clang
  __builtin_prefetch(ptr, 0, hint);
}

void OptimizationEngine::ProcessArrayWithPrefetch(int* array, size_t size) {
  constexpr size_t PREFETCH_DISTANCE = 8;  // Prefetch 8 элементов вперёд
  
  for (size_t i = 0; i < size; ++i) {
    // Предзагрузка данных
    if (i + PREFETCH_DISTANCE < size) {
      Prefetch(&array[i + PREFETCH_DISTANCE]);
    }
    
    // Обработка текущего элемента
    array[i] = array[i] * 2 + 1;
  }
}

// ============================================================================
// Проверка поддержки SIMD
// ============================================================================

bool OptimizationEngine::CheckAVX2Support() {
  unsigned int eax, ebx, ecx, edx;
  
  if (__get_cpuid(1, &eax, &ebx, &ecx, &edx)) {
    // Бит 28 в ECX - AVX
    bool avx = (ecx & (1 << 28)) != 0;
    
    if (avx && __get_cpuid(7, &eax, &ebx, &ecx, &edx)) {
      // Бит 5 в EBX - AVX2
      return (ebx & (1 << 5)) != 0;
    }
  }
  
  return false;
}

bool OptimizationEngine::CheckAVX512Support() {
  unsigned int eax, ebx, ecx, edx;
  
  if (__get_cpuid(7, &eax, &ebx, &ecx, &edx)) {
    // Бит 16 в EBX - AVX-512F (Foundation)
    return (ebx & (1 << 16)) != 0;
  }
  
  return false;
}

}  // namespace hardware_analysis

// ============================================================================
// Пример использования
// ============================================================================

int main() {
  using namespace hardware_analysis;
  
  std::cout << "=== Optimization Engine Demo ===\n\n";
  
  OptimizationEngine engine;
  
  // 1. DVFS оптимизация
  std::cout << "\n1. DVFS Frequency Optimization:\n";
  DVFSConfig config = {
    .min_frequency_mhz = 1000,
    .max_frequency_mhz = 4500,
    .target_temperature_celsius = 75.0,
    .power_limit_watts = 65.0
  };
  
  uint64_t optimal_freq = engine.CalculateOptimalFrequency(60.0, 70.0, config);
  std::cout << "Optimal frequency at 60% load, 70°C: " << optimal_freq << " MHz\n";
  
  optimal_freq = engine.CalculateOptimalFrequency(60.0, 85.0, config);
  std::cout << "Optimal frequency at 60% load, 85°C: " << optimal_freq 
            << " MHz (thermal throttling)\n";
  
  // 2. NUMA оптимизация
  std::cout << "\n2. NUMA Optimization:\n";
  int best_node = engine.FindBestNumaNode();
  std::cout << "Best NUMA node: " << best_node << "\n";
  
  // 3. Векторизованное суммирование
  std::cout << "\n3. Vectorized Operations (AVX2):\n";
  constexpr size_t SIZE = 1000000;
  double* data = new double[SIZE];
  for (size_t i = 0; i < SIZE; ++i) {
    data[i] = static_cast<double>(i);
  }
  
  double sum = engine.VectorizedSum_AVX2(data, SIZE);
  std::cout << "Sum of " << SIZE << " elements: " << sum << "\n";
  
  // 4. Prefetching
  std::cout << "\n4. Prefetching Demo:\n";
  int* int_array = new int[SIZE];
  for (size_t i = 0; i < SIZE; ++i) {
    int_array[i] = i;
  }
  
  engine.ProcessArrayWithPrefetch(int_array, SIZE);
  std::cout << "Array processed with prefetching\n";
  
  delete[] data;
  delete[] int_array;
  
  return 0;
}
