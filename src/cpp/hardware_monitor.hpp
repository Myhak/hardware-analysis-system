#ifndef HARDWARE_MONITOR_HPP
#define HARDWARE_MONITOR_HPP

#include <cstdint>
#include <string>
#include <vector>
#include <memory>
#include <stdexcept>

namespace hardware_analysis {

/**
 * @brief Исключение при работе с MSR регистрами
 */
class MSRException : public std::runtime_error {
 public:
  explicit MSRException(const std::string& message) 
    : std::runtime_error(message) {}
};

/**
 * @brief Метрики процессора
 */
struct CpuMetrics {
  int cpu_id;
  double temperature_celsius;
  uint64_t frequency_mhz;
  double voltage_volts;
  double power_watts;
  uint64_t timestamp_us;
};

/**
 * @brief Информация о NUMA узле
 */
struct NumaNode {
  int node_id;
  uint64_t memory_size_mb;
  std::vector<int> cpu_list;
  double memory_bandwidth_gbs;
};

/**
 * @brief S.M.A.R.T. атрибуты накопителя
 */
struct SmartAttributes {
  std::string device_path;
  uint64_t power_on_hours;
  uint64_t temperature_celsius;
  uint64_t total_bytes_written;
  uint64_t total_bytes_read;
  int health_percentage;
  uint64_t wear_leveling_count;
};

/**
 * @brief Класс для чтения MSR регистров Intel/AMD процессоров
 * 
 * Требует root привилегий и загруженного модуля ядра msr.
 * Загрузка: sudo modprobe msr
 */
class MSRReader {
 public:
  /**
   * @brief Конструктор для указанного CPU
   * @param cpu_id Номер процессора (0-based)
   * @throws MSRException если не удалось открыть /dev/cpu/X/msr
   */
  explicit MSRReader(int cpu_id);
  
  /**
   * @brief Деструктор, закрывает файловый дескриптор
   */
  ~MSRReader();

  // Запрет копирования
  MSRReader(const MSRReader&) = delete;
  MSRReader& operator=(const MSRReader&) = delete;

  /**
   * @brief Чтение 64-битного значения из MSR регистра
   * @param msr_addr Адрес MSR регистра (например, 0x19C для IA32_THERM_STATUS)
   * @return Значение из регистра
   * @throws MSRException при ошибке чтения
   */
  uint64_t Read(uint32_t msr_addr) const;

  /**
   * @brief Чтение температуры процессора
   * @return Температура в градусах Цельсия
   * @note Использует MSR_TEMPERATURE_TARGET (0x1A2) и IA32_THERM_STATUS (0x19C)
   */
  double ReadTemperature() const;

  /**
   * @brief Чтение текущей частоты процессора
   * @return Частота в МГц
   * @note Использует MSR_IA32_PERF_STATUS (0x198)
   */
  uint64_t ReadFrequency() const;

  /**
   * @brief Чтение энергопотребления через RAPL (Running Average Power Limit)
   * @return Энергопотребление в Ваттах
   * @note Использует MSR_PKG_ENERGY_STATUS (0x611)
   */
  double ReadPackagePower() const;

  /**
   * @brief Получение всех метрик процессора
   * @return Структура с метриками
   */
  CpuMetrics GetAllMetrics() const;

 private:
  int cpu_id_;
  int msr_fd_;  // Файловый дескриптор /dev/cpu/X/msr

  // MSR адреса для Intel процессоров
  static constexpr uint32_t MSR_IA32_THERM_STATUS = 0x19C;
  static constexpr uint32_t MSR_TEMPERATURE_TARGET = 0x1A2;
  static constexpr uint32_t MSR_IA32_PERF_STATUS = 0x198;
  static constexpr uint32_t MSR_PKG_ENERGY_STATUS = 0x611;
  static constexpr uint32_t MSR_RAPL_POWER_UNIT = 0x606;

  // Кэшированные значения для расчёта мощности
  mutable uint64_t last_energy_sample_;
  mutable uint64_t last_sample_time_us_;
};

/**
 * @brief Менеджер для мониторинга всех процессоров в системе
 */
class SystemMonitor {
 public:
  /**
   * @brief Конструктор, создаёт MSRReader для каждого CPU
   * @throws MSRException если нет доступа к MSR
   */
  SystemMonitor();

  /**
   * @brief Получение метрик для всех процессоров
   * @return Вектор метрик
   */
  std::vector<CpuMetrics> GetAllCpuMetrics() const;

  /**
   * @brief Получение информации о NUMA топологии
   * @return Вектор NUMA узлов
   */
  std::vector<NumaNode> GetNumaTopology() const;

  /**
   * @brief Чтение S.M.A.R.T. данных накопителя
   * @param device Путь к устройству (например, /dev/sda)
   * @return S.M.A.R.T. атрибуты
   * @note Требует smartmontools: sudo apt-get install smartmontools
   */
  SmartAttributes GetSmartData(const std::string& device) const;

  /**
   * @brief Получение количества процессоров в системе
   * @return Число CPU
   */
  int GetCpuCount() const { return cpu_count_; }

 private:
  int cpu_count_;
  std::vector<std::unique_ptr<MSRReader>> msr_readers_;

  /**
   * @brief Определение количества CPU в системе
   * @return Число процессоров
   */
  int DetectCpuCount() const;

  /**
   * @brief Парсинг /sys/devices/system/node для NUMA информации
   * @param node_id ID NUMA узла
   * @return Информация об узле
   */
  NumaNode ParseNumaNode(int node_id) const;
};

/**
 * @brief Утилитные функции
 */
namespace utils {
  /**
   * @brief Чтение целого числа из sysfs файла
   * @param path Путь к файлу
   * @return Значение
   */
  uint64_t ReadSysfsU64(const std::string& path);

  /**
   * @brief Чтение строки из sysfs файла
   * @param path Путь к файлу
   * @return Содержимое файла (без \n)
   */
  std::string ReadSysfsString(const std::string& path);

  /**
   * @brief Получение текущего времени в микросекундах
   * @return Timestamp в мкс
   */
  uint64_t GetTimestampUs();

  /**
   * @brief Проверка наличия модуля msr в ядре
   * @return true если модуль загружен
   */
  bool IsMSRModuleLoaded();

  /**
   * @brief Попытка загрузки модуля msr
   * @return true если успешно
   */
  bool LoadMSRModule();
}

}  // namespace hardware_analysis

#endif  // HARDWARE_MONITOR_HPP
