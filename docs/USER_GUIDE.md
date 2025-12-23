# Hardware Analysis System - User Guide

**Version:** 1.0  
**Last Updated:** December 23, 2025

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Installation](#installation)
3. [Quick Start Examples](#quick-start-examples)
4. [Features Guide](#features-guide)
5. [Troubleshooting](#troubleshooting)
6. [FAQ](#faq)

---

## Getting Started

### Prerequisites

**System Requirements:**
- Linux OS (Ubuntu 22.04+, RHEL 8+, Arch Linux)
- x86-64 CPU (Intel or AMD)
- 4 GB RAM minimum
- 1 GB free disk space
- Root access (for MSR operations)

**Software Dependencies:**
- GCC 11+ or Clang 14+
- CMake 3.15+
- .NET SDK 8.0+
- Python 3.9+

---

## Installation

### Method 1: Pre-built Packages (Recommended)

**Ubuntu/Debian:**
```bash
wget https://github.com/yourusername/hardware-analysis-system/releases/download/v1.0/has_1.0_amd64.deb
sudo dpkg -i has_1.0_amd64.deb
```

**RHEL/Fedora:**
```bash
sudo dnf install hardware-analysis-system-1.0.rpm
```

### Method 2: Build from Source

```bash
# Clone repository
git clone https://github.com/yourusername/hardware-analysis-system.git
cd hardware-analysis-system

# Install dependencies
sudo apt-get install build-essential cmake libboost-all-dev libnuma-dev
wget https://dot.net/v1/dotnet-install.sh && chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
pip3 install -r requirements.txt

# Build C++ components
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Release ..
make -j$(nproc)
sudo make install

# Build C# components
cd ../src/csharp
dotnet build -c Release
```

### Method 3: Docker (Isolated Environment)

```bash
docker pull ghcr.io/yourusername/hardware-analysis-system:latest
docker run -it --privileged hardware-analysis-system
```

**Note:** `--privileged` flag required for MSR access

---

## Quick Start Examples

### Example 1: Basic Hardware Monitoring

Monitor CPU, Memory, and Disk for 60 seconds:

```bash
cd src/csharp/Stage3_Monitoring
dotnet run -- --duration 60 --interval 1000
```

**Output:**
```
═══════════════════════════════════════════════════════
   Hardware Analysis System - Real-time Monitoring
═══════════════════════════════════════════════════════

[14:30:15] Started monitoring CPU (interval: 1000ms)
[14:30:15] Started monitoring Memory (interval: 1000ms)
[14:30:15] Started monitoring Disk (C:) (interval: 1000ms)

Sample 1:
  CPU 0: 65.2 °C, 3600 MHz, 0 W
  CPU 1: 63.8 °C, 3600 MHz, 0 W
  ...
```

### Example 2: Low-Level MSR Access

Read CPU temperature and frequency:

```bash
sudo ./build/stage2_hardware_monitor
```

**Output:**
```
=== Hardware Monitor (C++ Low-level Access) ===

Detected 8 CPU cores

Sample 1:
  CPU 0: 62.0 °C, 3400 MHz, 15.2 W
  CPU 1: 61.5 °C, 3300 MHz, 14.8 W
  ...
```

### Example 3: Cache Simulation

Simulate 64-block LRU cache with locality pattern:

```bash
python3 src/python/stage5_cache_simulator.py
```

**Output:**
```
═══════════════════════════════════════════════════════
           Cache Simulator (Stage 5)
═══════════════════════════════════════════════════════

1. Testing with locality pattern...
   Hit Rate: 78.42%
   Misses: 2,158

[Chart saved: cache_lru_performance.png]
```

### Example 4: ML Performance Prediction

Predict performance for a given configuration:

```bash
python3 src/python/stage6_ml_prediction.py
```

**Output:**
```
═══════════════════════════════════════════════════════
       ML Performance Predictor (Stage 6)
═══════════════════════════════════════════════════════

2. Training performance predictor...
   Training R²: 0.9245
   Test R²: 0.8932
   Test MAE: 245.73

4. Predicting performance for sample configurations...

   Budget Gaming PC       :  5234.8 points
   High-End Workstation   :  9487.3 points
```

---

## Features Guide

### Feature 1: Real-Time Monitoring with Alerts

Set up monitoring with custom alert thresholds:

```csharp
var config = new MonitoringConfig
{
    UpdateIntervalMs = 1000,
    EnableAlerts = true
};

var cpuMonitor = new CpuMonitor(config);
cpuMonitor.SetAlertThreshold("Temperature", 85.0, AlertSeverity.Warning);
cpuMonitor.SetAlertThreshold("Temperature", 95.0, AlertSeverity.Critical);

cpuMonitor.OnAlert += (sender, alert) =>
{
    if (alert.Severity == AlertSeverity.Critical)
    {
        Console.WriteLine($"CRITICAL: {alert.Message}");
        // Send email, trigger action, etc.
    }
};

cpuMonitor.StartMonitoring(1000);
```

### Feature 2: Performance Optimization

**DVFS (Dynamic Voltage and Frequency Scaling):**

```cpp
OptimizationEngine engine;

DVFSConfig config = {
    .min_frequency_mhz = 1000,
    .max_frequency_mhz = 4500,
    .target_temperature_celsius = 75.0,
    .power_limit_watts = 65.0
};

double current_load = 60.0;   // 60% CPU load
double current_temp = 70.0;   // 70°C

uint64_t optimal_freq = engine.CalculateOptimalFrequency(
    current_load, current_temp, config
);

// Apply frequency (requires root)
engine.SetCpuFrequency(0, optimal_freq);
```

**NUMA Optimization:**

```cpp
// Find best NUMA node
int best_node = engine.FindBestNumaNode();

// Bind current process
engine.BindProcessToNumaNode(getpid(), best_node);
```

### Feature 3: Generate Reports

**Text Report:**
```csharp
var generator = new ReportGenerator(monitors);
string report = generator.GenerateTextReport(startTime, endTime);
generator.SaveReport(report, "monitoring_report.txt");
```

**JSON Report:**
```csharp
string json = generator.GenerateJsonReport(startTime, endTime);
File.WriteAllText("monitoring_report.json", json);
```

**HTML Report with Charts:**
```csharp
string html = generator.GenerateHtmlReport(startTime, endTime);
File.WriteAllText("monitoring_report.html", html);
// Open in browser
Process.Start("xdg-open", "monitoring_report.html");
```

### Feature 4: Train Custom ML Models

```python
from ml_predictor import PerformancePredictor
import pandas as pd

# Load your dataset
df = pd.read_csv("my_benchmark_data.csv")
X = df[['cpu_cores', 'cpu_freq_mhz', 'ram_gb', 'ram_freq_mhz']].values
y = df['performance_score'].values

# Train model
predictor = PerformancePredictor(model_type='random_forest')
metrics = predictor.train(X, y, feature_names=df.columns.tolist())

print(f"Test R²: {metrics['test_r2']:.4f}")

# Save for later use
predictor.save('my_model.pkl')

# Load and predict
loaded_predictor = PerformancePredictor.load('my_model.pkl')
prediction = loaded_predictor.predict({
    'cpu_cores': 8,
    'cpu_freq_mhz': 3600,
    'ram_gb': 16,
    'ram_freq_mhz': 3200
})
```

---

## Troubleshooting

### Issue: "Failed to open /dev/cpu/0/msr"

**Cause:** MSR module not loaded or insufficient privileges

**Solution:**
```bash
# Load MSR module
sudo modprobe msr

# Verify
ls -l /dev/cpu/0/msr

# Run with sudo
sudo ./stage2_hardware_monitor
```

### Issue: "NUMA not available"

**Cause:** System doesn't support NUMA or libnuma not installed

**Solution:**
```bash
# Check NUMA support
numactl --hardware

# Install libnuma
sudo apt-get install libnuma-dev numactl
```

### Issue: "Model training R² < 0.7"

**Cause:** Insufficient or poor quality training data

**Solution:**
- Increase dataset size (>1000 samples)
- Check for data quality issues (missing values, outliers)
- Try different model types (random_forest, gradient_boosting)
- Perform feature engineering

### Issue: "Performance counter initialization failed"

**Cause:** Windows-specific issue with PerformanceCounter

**Solution:**
```bash
# Rebuild performance counter library
lodctr /r

# Or use alternative method
# Set environment variable to use fallback implementation
export USE_FALLBACK_COUNTERS=1
```

---

## FAQ

**Q: Can I use this system in production?**  
A: Yes, but avoid MSR writes in production. Use read-only monitoring mode.

**Q: How much overhead does monitoring add?**  
A: <5% CPU overhead at 1-second intervals. Can be reduced by increasing interval.

**Q: Can I monitor remote systems?**  
A: Not directly, but you can export metrics to Prometheus/Grafana for centralized monitoring.

**Q: Does it work on AMD CPUs?**  
A: Yes, MSR addresses are compatible with modern AMD Ryzen/EPYC processors.

**Q: Can I extend the system with custom monitors?**  
A: Yes! Implement `IHardwareMonitor` interface and inherit from `BaseAnalyzer`.

**Q: How accurate are ML predictions?**  
A: Typical R² > 0.85 on test data. Accuracy depends on training data quality.

**Q: Is GPU monitoring supported?**  
A: Basic metrics via system APIs. Full NVML/ROCm support planned for v2.0.

---

## Getting Help

- **Documentation:** https://github.com/yourusername/hardware-analysis-system/wiki
- **Issues:** https://github.com/yourusername/hardware-analysis-system/issues
- **Discussions:** https://github.com/yourusername/hardware-analysis-system/discussions
- **Email:** support@hardwareanalysis.com

---

**Happy analyzing!** 🚀
