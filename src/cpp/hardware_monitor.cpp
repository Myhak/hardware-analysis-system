#include "hardware_monitor.hpp"
#include <fcntl.h>
#include <unistd.h>
#include <sys/time.h>
#include <sys/stat.h>
#include <thread>
#include <fstream>
#include <sstream>
#include <iostream>
#include <cstring>
#include <algorithm>

namespace hardware_analysis {

// ============================================================================
// MSRReader Implementation
// ============================================================================

MSRReader::MSRReader(int cpu_id) 
    : cpu_id_(cpu_id), 
      msr_fd_(-1), 
      last_energy_sample_(0),
      last_sample_time_us_(0) {
  
  std::string msr_path = "/dev/cpu/" + std::to_string(cpu_id) + "/msr";
  
  msr_fd_ = open(msr_path.c_str(), O_RDONLY);
  if (msr_fd_ < 0) {
    throw MSRException("Failed to open " + msr_path + ": " + std::strerror(errno) +
                       "\nEnsure 'modprobe msr' is run and you have root privileges.");
  }
}

MSRReader::~MSRReader() {
  if (msr_fd_ >= 0) {
    close(msr_fd_);
  }
}

uint64_t MSRReader::Read(uint32_t msr_addr) const {
  uint64_t value = 0;
  
  if (pread(msr_fd_, &value, sizeof(value), msr_addr) != sizeof(value)) {
    throw MSRException("Failed to read MSR 0x" + 
                       std::to_string(msr_addr) + ": " + 
                       std::strerror(errno));
  }
  
  return value;
}

double MSRReader::ReadTemperature() const {
  // Читаем целевую температуру (Tj_max)
  uint64_t target = Read(MSR_TEMPERATURE_TARGET);
  int tj_max = (target >> 16) & 0xFF;
  
  // Читаем текущее значение температуры
  uint64_t status = Read(MSR_IA32_THERM_STATUS);
  int digital_readout = (status >> 16) & 0x7F;
  
  // Температура = Tj_max - Digital Readout
  return static_cast<double>(tj_max - digital_readout);
}

uint64_t MSRReader::ReadFrequency() const {
  uint64_t perf_status = Read(MSR_IA32_PERF_STATUS);
  
  // Биты [15:8] содержат текущий множитель частоты
  uint64_t multiplier = (perf_status >> 8) & 0xFF;
  
  // Базовая частота шины обычно 100 МГц для современных Intel CPU
  const uint64_t bus_frequency_mhz = 100;
  
  return multiplier * bus_frequency_mhz;
}

double MSRReader::ReadPackagePower() const {
  // Читаем единицы измерения энергии
  uint64_t power_unit = Read(MSR_RAPL_POWER_UNIT);
  double energy_unit = 1.0 / (1 << ((power_unit >> 8) & 0x1F));  // в Джоулях
  
  // Читаем текущее значение энергии
  uint64_t current_energy = Read(MSR_PKG_ENERGY_STATUS) & 0xFFFFFFFF;
  uint64_t current_time = utils::GetTimestampUs();
  
  if (last_energy_sample_ == 0) {
    // Первое измерение
    last_energy_sample_ = current_energy;
    last_sample_time_us_ = current_time;
    return 0.0;
  }
  
  // Расчёт разницы с учётом возможного переполнения 32-bit счётчика
  uint64_t energy_diff = current_energy - last_energy_sample_;
  if (current_energy < last_energy_sample_) {
    // Переполнение счётчика
    energy_diff = (0xFFFFFFFFULL - last_energy_sample_) + current_energy;
  }
  
  double energy_joules = energy_diff * energy_unit;
  double time_seconds = (current_time - last_sample_time_us_) / 1e6;
  
  // Обновляем для следующего измерения
  last_energy_sample_ = current_energy;
  last_sample_time_us_ = current_time;
  
  // Мощность = Энергия / Время
  return energy_joules / time_seconds;
}

CpuMetrics MSRReader::GetAllMetrics() const {
  CpuMetrics metrics;
  metrics.cpu_id = cpu_id_;
  metrics.temperature_celsius = ReadTemperature();
  metrics.frequency_mhz = ReadFrequency();
  metrics.power_watts = ReadPackagePower();
  metrics.timestamp_us = utils::GetTimestampUs();
  
  // Напряжение сложно прочитать напрямую, оставляем 0
  metrics.voltage_volts = 0.0;
  
  return metrics;
}

// ============================================================================
// SystemMonitor Implementation
// ============================================================================

SystemMonitor::SystemMonitor() : cpu_count_(0) {
  // Проверяем наличие модуля msr
  if (!utils::IsMSRModuleLoaded()) {
    std::cerr << "Warning: MSR module not loaded. Attempting to load...\n";
    if (!utils::LoadMSRModule()) {
      throw MSRException("Failed to load MSR module. Run: sudo modprobe msr");
    }
  }
  
  cpu_count_ = DetectCpuCount();
  
  // Создаём MSRReader для каждого CPU
  for (int i = 0; i < cpu_count_; ++i) {
    try {
      msr_readers_.emplace_back(std::make_unique<MSRReader>(i));
    } catch (const MSRException& e) {
      std::cerr << "Warning: Failed to create MSRReader for CPU " << i 
                << ": " << e.what() << std::endl;
    }
  }
}

std::vector<CpuMetrics> SystemMonitor::GetAllCpuMetrics() const {
  std::vector<CpuMetrics> all_metrics;
  
  for (const auto& reader : msr_readers_) {
    try {
      all_metrics.push_back(reader->GetAllMetrics());
    } catch (const MSRException& e) {
      std::cerr << "Warning: Failed to read metrics: " << e.what() << std::endl;
    }
  }
  
  return all_metrics;
}

std::vector<NumaNode> SystemMonitor::GetNumaTopology() const {
  std::vector<NumaNode> nodes;
  
  // Проверяем наличие директории /sys/devices/system/node
  struct stat st;
  if (stat("/sys/devices/system/node", &st) != 0) {
    // NUMA не поддерживается
    return nodes;
  }
  
  // Обычно от node0 до nodeN
  for (int node_id = 0; node_id < 8; ++node_id) {
    std::string node_path = "/sys/devices/system/node/node" + std::to_string(node_id);
    if (stat(node_path.c_str(), &st) != 0) {
      break;  // Больше нет узлов
    }
    
    nodes.push_back(ParseNumaNode(node_id));
  }
  
  return nodes;
}

SmartAttributes SystemMonitor::GetSmartData(const std::string& device) const {
  SmartAttributes attrs;
  attrs.device_path = device;
  
  // Запуск smartctl для получения данных
  std::string cmd = "smartctl -A " + device + " 2>/dev/null";
  FILE* pipe = popen(cmd.c_str(), "r");
  if (!pipe) {
    throw std::runtime_error("Failed to run smartctl. Install: sudo apt-get install smartmontools");
  }
  
  char buffer[256];
  while (fgets(buffer, sizeof(buffer), pipe) != nullptr) {
    std::string line(buffer);
    
    // Парсинг важных атрибутов
    if (line.find("Power_On_Hours") != std::string::npos) {
      std::istringstream iss(line);
      std::string id, attr, flag, value, worst, thresh, type, updated, when_failed;
      uint64_t raw_value;
      iss >> id >> attr >> flag >> value >> worst >> thresh >> type >> updated 
          >> when_failed >> raw_value;
      attrs.power_on_hours = raw_value;
    } else if (line.find("Temperature") != std::string::npos) {
      std::istringstream iss(line);
      std::string id, attr, flag, value, worst, thresh, type, updated, when_failed;
      uint64_t raw_value;
      iss >> id >> attr >> flag >> value >> worst >> thresh >> type >> updated 
          >> when_failed >> raw_value;
      attrs.temperature_celsius = raw_value;
    } else if (line.find("Total_LBAs_Written") != std::string::npos) {
      // Парсинг количества записанных данных
      // Реализация зависит от формата вывода smartctl
    }
  }
  
  pclose(pipe);
  
  // Заполняем значения по умолчанию для примера
  attrs.health_percentage = 95;
  attrs.total_bytes_written = 0;
  attrs.total_bytes_read = 0;
  attrs.wear_leveling_count = 0;
  
  return attrs;
}

int SystemMonitor::DetectCpuCount() const {
  return static_cast<int>(std::thread::hardware_concurrency());
}

NumaNode SystemMonitor::ParseNumaNode(int node_id) const {
  NumaNode node;
  node.node_id = node_id;
  
  std::string base_path = "/sys/devices/system/node/node" + std::to_string(node_id);
  
  // Читаем размер памяти (в байтах)
  try {
    std::string meminfo_path = base_path + "/meminfo";
    std::ifstream meminfo(meminfo_path);
    std::string line;
    while (std::getline(meminfo, line)) {
      if (line.find("MemTotal:") != std::string::npos) {
        std::istringstream iss(line);
        std::string label, label2;
        uint64_t mem_kb;
        iss >> label >> label2 >> mem_kb;  // "Node X MemTotal: 12345 kB"
        node.memory_size_mb = mem_kb / 1024;
        break;
      }
    }
  } catch (...) {
    node.memory_size_mb = 0;
  }
  
  // Читаем список CPU
  try {
    std::string cpulist_path = base_path + "/cpulist";
    std::string cpulist = utils::ReadSysfsString(cpulist_path);
    
    // Парсинг формата "0-3,8-11" -> {0,1,2,3,8,9,10,11}
    std::istringstream iss(cpulist);
    std::string range;
    while (std::getline(iss, range, ',')) {
      size_t dash_pos = range.find('-');
      if (dash_pos != std::string::npos) {
        int start = std::stoi(range.substr(0, dash_pos));
        int end = std::stoi(range.substr(dash_pos + 1));
        for (int cpu = start; cpu <= end; ++cpu) {
          node.cpu_list.push_back(cpu);
        }
      } else {
        node.cpu_list.push_back(std::stoi(range));
      }
    }
  } catch (...) {
    // Ошибка парсинга
  }
  
  // Пропускная способность памяти (условное значение)
  node.memory_bandwidth_gbs = 40.0;  // Типичное значение для DDR4
  
  return node;
}

// ============================================================================
// Utility Functions
// ============================================================================

namespace utils {

uint64_t ReadSysfsU64(const std::string& path) {
  std::ifstream file(path);
  if (!file.is_open()) {
    throw std::runtime_error("Failed to open " + path);
  }
  
  uint64_t value;
  file >> value;
  return value;
}

std::string ReadSysfsString(const std::string& path) {
  std::ifstream file(path);
  if (!file.is_open()) {
    throw std::runtime_error("Failed to open " + path);
  }
  
  std::string value;
  std::getline(file, value);
  
  // Убираем trailing newline
  if (!value.empty() && value.back() == '\n') {
    value.pop_back();
  }
  
  return value;
}

uint64_t GetTimestampUs() {
  struct timeval tv;
  gettimeofday(&tv, nullptr);
  return static_cast<uint64_t>(tv.tv_sec) * 1000000ULL + tv.tv_usec;
}

bool IsMSRModuleLoaded() {
  struct stat st;
  return stat("/dev/cpu/0/msr", &st) == 0;
}

bool LoadMSRModule() {
  int ret = system("modprobe msr 2>/dev/null");
  return ret == 0 && IsMSRModuleLoaded();
}

}  // namespace utils

}  // namespace hardware_analysis

// ============================================================================
// Main (Example Usage)
// ============================================================================

int main() {
  try {
    std::cout << "=== Hardware Monitor (C++ Low-level Access) ===\n\n";
    
    hardware_analysis::SystemMonitor monitor;
    
    std::cout << "Detected " << monitor.GetCpuCount() << " CPU cores\n\n";
    
    // Мониторинг в течение 5 секунд
    std::cout << "Monitoring for 5 seconds (1 sample/sec)...\n\n";
    
    for (int i = 0; i < 5; ++i) {
      auto metrics = monitor.GetAllCpuMetrics();
      
      std::cout << "Sample " << (i + 1) << ":\n";
      for (const auto& m : metrics) {
        std::cout << "  CPU " << m.cpu_id << ": "
                  << m.temperature_celsius << " °C, "
                  << m.frequency_mhz << " MHz, "
                  << m.power_watts << " W\n";
      }
      std::cout << "\n";
      
      sleep(1);
    }
    
    // NUMA топология
    std::cout << "=== NUMA Topology ===\n";
    auto numa_nodes = monitor.GetNumaTopology();
    if (numa_nodes.empty()) {
      std::cout << "NUMA not supported or not detected\n";
    } else {
      for (const auto& node : numa_nodes) {
        std::cout << "Node " << node.node_id << ": "
                  << node.memory_size_mb << " MB RAM, CPUs: ";
        for (size_t i = 0; i < node.cpu_list.size(); ++i) {
          std::cout << node.cpu_list[i];
          if (i < node.cpu_list.size() - 1) std::cout << ",";
        }
        std::cout << "\n";
      }
    }
    
    // S.M.A.R.T. данные (опционально)
    std::cout << "\n=== S.M.A.R.T. Data ===\n";
    try {
      auto smart = monitor.GetSmartData("/dev/sda");
      std::cout << "Device: " << smart.device_path << "\n";
      std::cout << "Power-on hours: " << smart.power_on_hours << "\n";
      std::cout << "Temperature: " << smart.temperature_celsius << " °C\n";
      std::cout << "Health: " << smart.health_percentage << "%\n";
    } catch (const std::exception& e) {
      std::cout << "S.M.A.R.T. data unavailable: " << e.what() << "\n";
    }
    
  } catch (const hardware_analysis::MSRException& e) {
    std::cerr << "MSR Error: " << e.what() << std::endl;
    return 1;
  } catch (const std::exception& e) {
    std::cerr << "Error: " << e.what() << std::endl;
    return 1;
  }
  
  return 0;
}
