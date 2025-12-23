using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO;

namespace HardwareAnalysis.Stage1
{
    /// <summary>
    /// Результаты бенчмарка памяти
    /// </summary>
    public class MemoryBenchmarkResult
    {
        public string TestName { get; set; }
        public double TimeMs { get; set; }
        public double BandwidthGBs { get; set; }
        public double LatencyNs { get; set; }
        public long DataSizeMB { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Симулятор работы с памятью с расширенными возможностями
    /// </summary>
    public class AdvancedMemorySimulator
    {
        private const int MB = 1024 * 1024;
        private const int KB = 1024;
        private const int CACHE_LINE_SIZE = 64;

        public AdvancedMemorySimulator()
        {
            Console.WriteLine("=== Advanced Memory Simulator ===");
            Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
            Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        }

        /// <summary>
        /// Последовательный доступ к памяти
        /// </summary>
        public MemoryBenchmarkResult MeasureSequentialAccess(int[] array)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = Stopwatch.StartNew();
            long sum = 0;
            
            for (int i = 0; i < array.Length; i++)
                sum += array[i];
            
            sw.Stop();

            double timeSec = sw.Elapsed.TotalSeconds;
            double bandwidthGBs = (array.Length * sizeof(int)) / (1e9 * timeSec);
            double latencyNs = (sw.Elapsed.TotalMilliseconds * 1e6) / array.Length;

            return new MemoryBenchmarkResult
            {
                TestName = "Sequential Access",
                TimeMs = sw.Elapsed.TotalMilliseconds,
                BandwidthGBs = bandwidthGBs,
                LatencyNs = latencyNs,
                DataSizeMB = (array.Length * sizeof(int)) / MB,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Случайный доступ к памяти
        /// </summary>
        public MemoryBenchmarkResult MeasureRandomAccess(int[] array)
        {
            var indices = new int[array.Length];
            var rng = new Random(42);
            for (int i = 0; i < indices.Length; i++)
                indices[i] = rng.Next(array.Length);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = Stopwatch.StartNew();
            long sum = 0;
            
            for (int i = 0; i < indices.Length; i++)
                sum += array[indices[i]];
            
            sw.Stop();

            double timeSec = sw.Elapsed.TotalSeconds;
            double bandwidthGBs = (array.Length * sizeof(int)) / (1e9 * timeSec);
            double latencyNs = (sw.Elapsed.TotalMilliseconds * 1e6) / array.Length;

            return new MemoryBenchmarkResult
            {
                TestName = "Random Access",
                TimeMs = sw.Elapsed.TotalMilliseconds,
                BandwidthGBs = bandwidthGBs,
                LatencyNs = latencyNs,
                DataSizeMB = (array.Length * sizeof(int)) / MB,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Измерение латентности кэша L1/L2/L3
        /// </summary>
        public void MeasureCacheLatency()
        {
            Console.WriteLine("\n=== Cache Latency Test ===");

            // L1 cache: 32 KB (обычно)
            MeasureCacheLevel("L1", 8 * KB / sizeof(int));

            // L2 cache: 256 KB (обычно)
            MeasureCacheLevel("L2", 64 * KB / sizeof(int));

            // L3 cache: 8 MB (обычно)
            MeasureCacheLevel("L3", 2 * MB / sizeof(int));

            // Main memory
            MeasureCacheLevel("Main Memory", 64 * MB / sizeof(int));
        }

        private void MeasureCacheLevel(string levelName, int arraySize)
        {
            var array = new int[arraySize];
            for (int i = 0; i < array.Length; i++)
                array[i] = i;

            const int iterations = 10000;
            var sw = Stopwatch.StartNew();

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < array.Length; i++)
                    array[i] = array[i] + 1;
            }

            sw.Stop();

            double latencyNs = (sw.Elapsed.TotalMilliseconds * 1e6) / (iterations * array.Length);
            Console.WriteLine($"{levelName,-15} Size: {arraySize * sizeof(int) / KB,6} KB, Latency: {latencyNs:F2} ns");
        }

        /// <summary>
        /// Тест производительности при разных шагах доступа (stride)
        /// </summary>
        public void MeasureStrideAccess(int[] array)
        {
            Console.WriteLine("\n=== Stride Access Pattern Test ===");

            int[] strides = { 1, 2, 4, 8, 16, 32, 64 };

            foreach (int stride in strides)
            {
                GC.Collect();
                var sw = Stopwatch.StartNew();
                long sum = 0;

                for (int i = 0; i < array.Length; i += stride)
                    sum += array[i];

                sw.Stop();

                int accessedElements = (array.Length + stride - 1) / stride;
                double bandwidthGBs = (accessedElements * sizeof(int)) / (1e9 * sw.Elapsed.TotalSeconds);
                
                Console.WriteLine($"Stride: {stride,3} elements, Bandwidth: {bandwidthGBs:F2} GB/s, " +
                                  $"Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
            }
        }

        /// <summary>
        /// Расчёт теоретической пропускной способности
        /// </summary>
        public double CalculateTheoreticalBandwidth(string memoryType, int channels)
        {
            double bandwidth = 0.0;

            switch (memoryType.ToUpper())
            {
                case "DDR4-2400":
                    bandwidth = 19.2;
                    break;
                case "DDR4-3200":
                    bandwidth = 25.6;
                    break;
                case "DDR4-3600":
                    bandwidth = 28.8;
                    break;
                case "DDR5-4800":
                    bandwidth = 38.4;
                    break;
                case "DDR5-6000":
                    bandwidth = 48.0;
                    break;
                case "DDR5-8000":
                    bandwidth = 64.0;
                    break;
                default:
                    throw new ArgumentException($"Unknown memory type: {memoryType}");
            }

            return bandwidth * channels;
        }

        /// <summary>
        /// Сохранение результатов в JSON
        /// </summary>
        public void SaveResults(MemoryBenchmarkResult[] results, string filename)
        {
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            File.WriteAllText(filename, json);
            Console.WriteLine($"\nResults saved to: {filename}");
        }

        /// <summary>
        /// Генерация отчёта в формате CSV
        /// </summary>
        public void GenerateCSVReport(MemoryBenchmarkResult[] results, string filename)
        {
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine("TestName,TimeMs,BandwidthGBs,LatencyNs,DataSizeMB,Timestamp");
                
                foreach (var result in results)
                {
                    writer.WriteLine($"{result.TestName},{result.TimeMs:F2},{result.BandwidthGBs:F2}," +
                                     $"{result.LatencyNs:F2},{result.DataSizeMB},{result.Timestamp:O}");
                }
            }
            
            Console.WriteLine($"CSV report saved to: {filename}");
        }

        public static void Main(string[] args)
        {
            var sim = new AdvancedMemorySimulator();
            
            // Размер массива: 256 MB
            int arraySize = 256 * MB / sizeof(int);
            var array = new int[arraySize];

            Console.WriteLine($"\nArray size: {arraySize * sizeof(int) / MB} MB ({arraySize:N0} elements)");
            Console.WriteLine("Initializing array...");

            // Заполнение массива
            for (int i = 0; i < array.Length; i++)
                array[i] = i ^ 0xCAFEBABE;

            Console.WriteLine("\n=== Memory Bandwidth Benchmark ===");

            var results = new MemoryBenchmarkResult[2];

            // Последовательный доступ
            Console.WriteLine("\nRunning sequential access test...");
            results[0] = sim.MeasureSequentialAccess(array);
            Console.WriteLine($"[Sequential] Time: {results[0].TimeMs:F2} ms, " +
                              $"Bandwidth: {results[0].BandwidthGBs:F2} GB/s, " +
                              $"Latency: {results[0].LatencyNs:F2} ns/access");

            // Случайный доступ
            Console.WriteLine("\nRunning random access test...");
            results[1] = sim.MeasureRandomAccess(array);
            Console.WriteLine($"[Random]     Time: {results[1].TimeMs:F2} ms, " +
                              $"Bandwidth: {results[1].BandwidthGBs:F2} GB/s, " +
                              $"Latency: {results[1].LatencyNs:F2} ns/access");

            // Теоретическая пропускная способность
            Console.WriteLine("\n=== Theoretical Bandwidth ===");
            string[] memTypes = { "DDR4-3200", "DDR5-4800", "DDR5-6000" };
            int[] channelConfigs = { 1, 2, 4 };

            foreach (var memType in memTypes)
            {
                foreach (var channels in channelConfigs)
                {
                    double theoretical = sim.CalculateTheoreticalBandwidth(memType, channels);
                    Console.WriteLine($"{memType} x{channels} channels: {theoretical:F1} GB/s");
                }
            }

            // Измерение латентности кэша
            sim.MeasureCacheLatency();

            // Тест stride доступа
            sim.MeasureStrideAccess(array);

            // Анализ эффективности
            Console.WriteLine("\n=== Efficiency Analysis ===");
            double seqBandwidth = results[0].BandwidthGBs;
            double randBandwidth = results[1].BandwidthGBs;
            double efficiency = (randBandwidth / seqBandwidth) * 100;
            
            Console.WriteLine($"Sequential bandwidth: {seqBandwidth:F2} GB/s");
            Console.WriteLine($"Random bandwidth:     {randBandwidth:F2} GB/s");
            Console.WriteLine($"Random efficiency:    {efficiency:F1}% of sequential");
            Console.WriteLine($"Performance loss:     {100 - efficiency:F1}% (due to cache misses and DRAM latency)");

            // Сохранение результатов
            sim.SaveResults(results, "memory_benchmark_results.json");
            sim.GenerateCSVReport(results, "memory_benchmark_results.csv");

            Console.WriteLine("\n=== Benchmark Complete ===");
        }
    }
}
