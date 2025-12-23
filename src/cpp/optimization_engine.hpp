#ifndef OPTIMIZATION_ENGINE_HPP
#define OPTIMIZATION_ENGINE_HPP

#include <vector>
#include <cstdint>
#include <memory>
#include <immintrin.h>  // AVX/AVX2/AVX-512

namespace hardware_analysis {

/**
 * @brief Конфигурация DVFS (Dynamic Voltage and Frequency Scaling)
 */
struct DVFSConfig {
  uint64_t min_frequency_mhz;
  uint64_t max_frequency_mhz;
  double target_temperature_celsius;
  double power_limit_watts;
};

/**
 * @brief Движок оптимизации производительности
 */
class OptimizationEngine {
 public:
  OptimizationEngine();

  // ========== DVFS оптимизация ==========
  
  /**
   * @brief Адаптивное управление частотой на основе загрузки
   * @param current_load_percent Текущая загрузка CPU (0-100)
   * @param current_temp_celsius Текущая температура
   * @return Рекомендуемая частота в МГц
   */
  uint64_t CalculateOptimalFrequency(
      double current_load_percent,
      double current_temp_celsius,
      const DVFSConfig& config);

  /**
   * @brief Установка частоты процессора (требует root)
   * @param cpu_id ID процессора
   * @param frequency_mhz Целевая частота
   * @return true если успешно
   */
  bool SetCpuFrequency(int cpu_id, uint64_t frequency_mhz);

  // ========== NUMA оптимизация ==========
  
  /**
   * @brief Привязка процесса к оптимальному NUMA узлу
   * @param pid ID процесса
   * @param numa_node Номер NUMA узла
   * @return true если успешно
   */
  bool BindProcessToNumaNode(pid_t pid, int numa_node);

  /**
   * @brief Выбор оптимального NUMA узла на основе загрузки памяти
   * @return Номер оптимального узла
   */
  int FindBestNumaNode();

  // ========== Векторизация ==========
  
  /**
   * @brief Векторизованное суммирование массива (AVX2)
   * @param array Массив чисел
   * @param size Размер массива
   * @return Сумма элементов
   */
  double VectorizedSum_AVX2(const double* array, size_t size);

  /**
   * @brief Векторизованное умножение матриц (AVX2)
   * @param A Матрица A (M x K)
   * @param B Матрица B (K x N)
   * @param C Результирующая матрица (M x N)
   * @param M, K, N Размерности
   */
  void MatrixMultiply_AVX2(
      const double* A, const double* B, double* C,
      size_t M, size_t K, size_t N);

  // ========== Cache-friendly структуры ==========
  
  /**
   * @brief Оптимизированная структура данных с выравниванием по cache line
   * 
   * Пример: вместо struct { int a; int b; } использовать:
   * alignas(64) struct CacheFriendly { int a; char pad1[60]; int b; char pad2[60]; }
   */
  template<typename T>
  class CacheAlignedVector {
   public:
    CacheAlignedVector(size_t size);
    ~CacheAlignedVector();
    
    T& operator[](size_t index);
    const T& operator[](size_t index) const;
    
    size_t size() const { return size_; }
    
   private:
    T* data_;
    size_t size_;
    static constexpr size_t CACHE_LINE_SIZE = 64;
  };

  // ========== Prefetching ==========
  
  /**
   * @brief Предварительная загрузка данных в кэш
   * @param ptr Указатель на данные
   * @param hint Уровень кэша (0=L1, 1=L2, 2=L3, 3=не кэшируется)
   */
  static void Prefetch(const void* ptr, int hint = 0);

  /**
   * @brief Оптимизированный цикл с prefetching
   */
  void ProcessArrayWithPrefetch(int* array, size_t size);

 private:
  /**
   * @brief Проверка поддержки AVX2
   */
  bool CheckAVX2Support();

  /**
   * @brief Проверка поддержки AVX-512
   */
  bool CheckAVX512Support();

  bool avx2_supported_;
  bool avx512_supported_;
};

// ========== Реализация шаблонных функций ==========

template<typename T>
OptimizationEngine::CacheAlignedVector<T>::CacheAlignedVector(size_t size)
    : size_(size) {
  // Выделяем выровненную память
  posix_memalign(reinterpret_cast<void**>(&data_), CACHE_LINE_SIZE, 
                 size * sizeof(T));
}

template<typename T>
OptimizationEngine::CacheAlignedVector<T>::~CacheAlignedVector() {
  free(data_);
}

template<typename T>
T& OptimizationEngine::CacheAlignedVector<T>::operator[](size_t index) {
  return data_[index];
}

template<typename T>
const T& OptimizationEngine::CacheAlignedVector<T>::operator[](size_t index) const {
  return data_[index];
}

}  // namespace hardware_analysis

#endif  // OPTIMIZATION_ENGINE_HPP
