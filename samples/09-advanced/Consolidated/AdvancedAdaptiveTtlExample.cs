// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Caching.Abstractions;
using Excalibur.Dispatch.CloudNative.Caching.AdaptiveTtl;
using Excalibur.Dispatch.CloudNative.Caching.AdaptiveTtl.Strategies;
using Excalibur.Dispatch.CloudNative.Caching.Providers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace examples.Consolidated;

/// <summary>
/// Demonstrates advanced adaptive TTL caching patterns for optimizing cache performance.
/// </summary>
public class AdvancedAdaptiveTtlExample {
 private readonly IServiceProvider _serviceProvider;
 private readonly ILogger<AdvancedAdaptiveTtlExample> _logger;

 /// <summary>
 /// Initializes a new instance of the <see cref="AdvancedAdaptiveTtlExample"/> class.
 /// </summary>
 public AdvancedAdaptiveTtlExample()
 {
 var services = new ServiceCollection();
 ConfigureServices(services);
 _serviceProvider = services.BuildServiceProvider();
 _logger = _serviceProvider.GetRequiredService<ILogger<AdvancedAdaptiveTtlExample>>();
 }

 /// <summary>
 /// Runs all adaptive TTL examples.
 /// </summary>
 public async Task RunAsync()
 {
 _logger.LogInformation("Starting Advanced Adaptive TTL Examples");

 await DemonstrateHitRateBasedStrategy();
 await DemonstrateFrequencyBasedStrategy();
 await DemonstrateMemoryPressureStrategy();
 await DemonstrateTimePatternStrategy();
 await DemonstrateCompositeStrategy();
 await DemonstrateRealWorldScenario();
 await RunPerformanceBenchmark();

 _logger.LogInformation("Completed Advanced Adaptive TTL Examples");
 }

 /// <summary>
 /// Demonstrates hit rate-based TTL adaptation.
 /// </summary>
 private async Task DemonstrateHitRateBasedStrategy()
 {
 _logger.LogInformation("\n=== Hit Rate-Based TTL Strategy ===");

 var cache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var strategy = new HitRateBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions { TargetHitRate = 0.8 }),
 _serviceProvider.GetRequiredService<ITtlMetricsCollector>());

 // Simulate varying access patterns
 var keys = Enumerable.Range(1, 10).Select(i => $"hitrate-key-{i}").ToList();

 // Initial population
 foreach (var key in keys)
 {
 await cache.SetAsync(key, GenerateData(key), new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(5),
 AdaptiveTtlStrategy = strategy
 });
 }

 // Simulate different hit rates
 var random = new Random();
 for (int i = 0; i < 100; i++)
 {
 var key = keys[random.Next(keys.Count)];
 var result = await cache.GetAsync<string>(key);

 if (i % 20 == 0)
 {
 var metrics = await cache.GetMetricsAsync(key);
 _logger.LogInformation(
 "Key {Key} - Hit Rate: {HitRate:P2}, Current TTL: {Ttl}",
 key, metrics.HitRate, metrics.CurrentTtl);
 }

 await Task.Delay(50);
 }

 // Show final statistics
 foreach (var key in keys.Take(3))
 {
 var metrics = await cache.GetMetricsAsync(key);
 _logger.LogInformation(
 "Final stats for {Key} - Hit Rate: {HitRate:P2}, TTL: {Ttl}, Accesses: {Accesses}",
 key, metrics.HitRate, metrics.CurrentTtl, metrics.TotalAccesses);
 }
 }

 /// <summary>
 /// Demonstrates frequency-based TTL adaptation.
 /// </summary>
 private async Task DemonstrateFrequencyBasedStrategy()
 {
 _logger.LogInformation("\n=== Frequency-Based TTL Strategy ===");

 var cache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var strategy = new FrequencyBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 _serviceProvider.GetRequiredService<ITtlMetricsCollector>());

 // Create keys with different access frequencies
 var hotKey = "frequency-hot";
 var warmKey = "frequency-warm";
 var coldKey = "frequency-cold";

 var options = new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(5),
 AdaptiveTtlStrategy = strategy
 };

 await cache.SetAsync(hotKey, GenerateData(hotKey), options);
 await cache.SetAsync(warmKey, GenerateData(warmKey), options);
 await cache.SetAsync(coldKey, GenerateData(coldKey), options);

 // Simulate access patterns
 var tasks = new List<Task>();

 // Hot key - accessed frequently
 tasks.Add(Task.Run(async () =>
 {
 for (int i = 0; i < 50; i++)
 {
 await cache.GetAsync<string>(hotKey);
 await Task.Delay(100);
 }
 }));

 // Warm key - accessed moderately
 tasks.Add(Task.Run(async () =>
 {
 for (int i = 0; i < 20; i++)
 {
 await cache.GetAsync<string>(warmKey);
 await Task.Delay(250);
 }
 }));

 // Cold key - accessed rarely
 tasks.Add(Task.Run(async () =>
 {
 for (int i = 0; i < 5; i++)
 {
 await cache.GetAsync<string>(coldKey);
 await Task.Delay(1000);
 }
 }));

 await Task.WhenAll(tasks);

 // Display frequency-based TTL adjustments
 foreach (var key in new[] { hotKey, warmKey, coldKey })
 {
 var metrics = await cache.GetMetricsAsync(key);
 _logger.LogInformation(
 "{Key} - Access Frequency: {Frequency:F2}/min, Adjusted TTL: {Ttl}",
 key, metrics.AccessFrequency * 60, metrics.CurrentTtl);
 }
 }

 /// <summary>
 /// Demonstrates memory pressure-based TTL adaptation.
 /// </summary>
 private async Task DemonstrateMemoryPressureStrategy()
 {
 _logger.LogInformation("\n=== Memory Pressure-Based TTL Strategy ===");

 var cache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var strategy = new MemoryPressureTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 _serviceProvider.GetRequiredService<ITtlMetricsCollector>());

 // Simulate different memory pressure scenarios
 var scenarios = new[]
 {
 (pressure: 0.3, label: "Low"),
 (pressure: 0.6, label: "Moderate"),
 (pressure: 0.85, label: "High"),
 (pressure: 0.95, label: "Critical")
 };

 foreach (var scenario in scenarios)
 {
 // Simulate memory pressure
 strategy.SimulateMemoryPressure(scenario.pressure);

 var key = $"memory-{scenario.label.ToLower()}";
 var options = new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(10),
 AdaptiveTtlStrategy = strategy
 };

 await cache.SetAsync(key, GenerateData(key), options);

 var metrics = await cache.GetMetricsAsync(key);
 _logger.LogInformation(
 "Memory Pressure: {Pressure} ({Label}) - Adjusted TTL: {Ttl} (from base: 10 min)",
 scenario.label, scenario.pressure, metrics.CurrentTtl);
 }
 }

 /// <summary>
 /// Demonstrates time pattern-based TTL adaptation.
 /// </summary>
 private async Task DemonstrateTimePatternStrategy()
 {
 _logger.LogInformation("\n=== Time Pattern-Based TTL Strategy ===");

 var cache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var strategy = new TimePatternTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 _serviceProvider.GetRequiredService<ITtlMetricsCollector>());

 // Configure time patterns
 strategy.AddPattern(new TimePattern
 {
 Name = "BusinessHours",
 DaysOfWeek = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
 DayOfWeek.Thursday, DayOfWeek.Friday },
 StartHour = 9,
 EndHour = 17,
 TtlMultiplier = 0.5 // Shorter TTL during business hours
 });

 strategy.AddPattern(new TimePattern
 {
 Name = "PeakHours",
 DaysOfWeek = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
 DayOfWeek.Thursday, DayOfWeek.Friday },
 StartHour = 11,
 EndHour = 14,
 TtlMultiplier = 0.3 // Even shorter during peak
 });

 strategy.AddPattern(new TimePattern
 {
 Name = "Weekend",
 DaysOfWeek = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday },
 StartHour = 0,
 EndHour = 24,
 TtlMultiplier = 2.0 // Longer TTL on weekends
 });

 // Test different times
 var testTimes = new[]
 {
 (time: new DateTime(2025, 7, 21, 10, 0, 0), desc: "Monday 10 AM"),
 (time: new DateTime(2025, 7, 21, 12, 30, 0), desc: "Monday 12:30 PM (Peak)"),
 (time: new DateTime(2025, 7, 21, 18, 0, 0), desc: "Monday 6 PM"),
 (time: new DateTime(2025, 7, 26, 14, 0, 0), desc: "Saturday 2 PM")
 };

 foreach (var test in testTimes)
 {
 strategy.SetCurrentTime(test.time);

 var key = $"time-{test.time:HHmm}";
 var options = new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(30),
 AdaptiveTtlStrategy = strategy
 };

 await cache.SetAsync(key, GenerateData(key), options);

 var metrics = await cache.GetMetricsAsync(key);
 _logger.LogInformation(
 "{Description} - Adjusted TTL: {Ttl} (base: 30 min)",
 test.desc, metrics.CurrentTtl);
 }
 }

 /// <summary>
 /// Demonstrates composite strategy combining multiple approaches.
 /// </summary>
 private async Task DemonstrateCompositeStrategy()
 {
 _logger.LogInformation("\n=== Composite TTL Strategy ===");

 var cache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var metricsCollector = _serviceProvider.GetRequiredService<ITtlMetricsCollector>();

 // Create individual strategies
 var hitRateStrategy = new HitRateBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions { TargetHitRate = 0.8 }),
 metricsCollector);

 var frequencyStrategy = new FrequencyBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector);

 var memoryStrategy = new MemoryPressureTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector);

 // Create composite strategy with weights
 var compositeStrategy = new CompositeTtlStrategy(metricsCollector);
 compositeStrategy.AddStrategy(hitRateStrategy, 0.4); // 40% weight
 compositeStrategy.AddStrategy(frequencyStrategy, 0.4); // 40% weight
 compositeStrategy.AddStrategy(memoryStrategy, 0.2); // 20% weight

 // Test composite strategy
 var key = "composite-test";
 var options = new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(10),
 AdaptiveTtlStrategy = compositeStrategy
 };

 await cache.SetAsync(key, GenerateData(key), options);

 // Simulate various conditions
 memoryStrategy.SimulateMemoryPressure(0.7);

 for (int i = 0; i < 20; i++)
 {
 var result = await cache.GetAsync<string>(key);
 await Task.Delay(100);
 }

 var metrics = await cache.GetMetricsAsync(key);
 var breakdown = compositeStrategy.GetStrategyBreakdown(key);

 _logger.LogInformation("Composite Strategy Results:");
 _logger.LogInformation(" Final TTL: {Ttl}", metrics.CurrentTtl);
 _logger.LogInformation(" Strategy Contributions:");
 foreach (var contribution in breakdown)
 {
 _logger.LogInformation(
 " {Strategy}: {Ttl} (weight: {Weight:P0})",
 contribution.StrategyName, contribution.CalculatedTtl, contribution.Weight);
 }
 }

 /// <summary>
 /// Demonstrates a real-world e-commerce scenario.
 /// </summary>
 private async Task DemonstrateRealWorldScenario()
 {
 _logger.LogInformation("\n=== Real-World E-Commerce Scenario ===");

 var cache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var metricsCollector = _serviceProvider.GetRequiredService<ITtlMetricsCollector>();

 // Create strategies for different data types
 var productCatalogStrategy = CreateProductCatalogStrategy(metricsCollector);
 var userSessionStrategy = CreateUserSessionStrategy(metricsCollector);
 var inventoryStrategy = CreateInventoryStrategy(metricsCollector);
 var recommendationStrategy = CreateRecommendationStrategy(metricsCollector);

 // Simulate different types of cache entries
 var scenarios = new[]
 {
 (key: "product:bestseller:123", data: "iPhone 15 Pro", strategy: productCatalogStrategy, desc: "Best Seller Product"),
 (key: "product:regular:456", data: "USB Cable", strategy: productCatalogStrategy, desc: "Regular Product"),
 (key: "session:user:789", data: "UserSession{cart:3}", strategy: userSessionStrategy, desc: "User Session"),
 (key: "inventory:hot:123", data: "Stock: 5", strategy: inventoryStrategy, desc: "Hot Item Inventory"),
 (key: "recommendations:user:789", data: "Related: [...]", strategy: recommendationStrategy, desc: "User Recommendations")
 };

 // Initial cache population
 foreach (var scenario in scenarios)
 {
 var options = new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(15),
 AdaptiveTtlStrategy = scenario.strategy,
 Tags = new[] { "ecommerce", scenario.key.Split(':')[0] }
 };

 await cache.SetAsync(scenario.key, scenario.data, options);
 _logger.LogInformation("Cached: {Description} with key {Key}", scenario.desc, scenario.key);
 }

 // Simulate Black Friday traffic pattern
 _logger.LogInformation("\nSimulating Black Friday traffic surge...");

 var tasks = new List<Task>();
 var cts = new CancellationTokenSource();

 // Simulate high traffic on bestseller
 tasks.Add(SimulateTraffic(cache, "product:bestseller:123", 100, 50, cts.Token));

 // Moderate traffic on regular product
 tasks.Add(SimulateTraffic(cache, "product:regular:456", 20, 200, cts.Token));

 // Session updates
 tasks.Add(SimulateTraffic(cache, "session:user:789", 50, 100, cts.Token));

 // Inventory checks for hot item
 tasks.Add(SimulateTraffic(cache, "inventory:hot:123", 200, 25, cts.Token));

 // Run for 5 seconds
 await Task.Delay(5000);
 cts.Cancel();

 try
 {
 await Task.WhenAll(tasks);
 }
 catch (OperationCanceledException)
 {
 // Expected when cancellation is requested during shutdown
 _logger.LogDebug("Task cancellation requested during adaptive TTL benchmark");
 }

 // Display adaptive results
 _logger.LogInformation("\nAdaptive TTL Results:");
 foreach (var scenario in scenarios)
 {
 var metrics = await cache.GetMetricsAsync(scenario.key);
 _logger.LogInformation(
 "{Description}:",
 scenario.desc);
 _logger.LogInformation(
 " Key: {Key}",
 scenario.key);
 _logger.LogInformation(
 " Hit Rate: {HitRate:P2}, Access Freq: {Freq:F1}/min",
 metrics.HitRate, metrics.AccessFrequency * 60);
 _logger.LogInformation(
 " Original TTL: 15 min → Adapted TTL: {Ttl}",
 metrics.CurrentTtl);
 _logger.LogInformation(
 " Total Accesses: {Accesses}, Size: {Size} bytes",
 metrics.TotalAccesses, metrics.EntrySize);
 }
 }

 /// <summary>
 /// Runs performance benchmarks comparing adaptive vs fixed TTL.
 /// </summary>
 private async Task RunPerformanceBenchmark()
 {
 _logger.LogInformation("\n=== Performance Benchmark: Adaptive vs Fixed TTL ===");

 const int numKeys = 100;
 const int numOperations = 10000;
 const int hotKeyCount = 10;

 // Benchmark fixed TTL
 var fixedCache = _serviceProvider.GetRequiredService<IDistributedCache>();
 var fixedMetrics = await BenchmarkCache(
 fixedCache,
 numKeys,
 numOperations,
 hotKeyCount,
 useAdaptive: false);

 // Benchmark adaptive TTL
 var adaptiveCache = _serviceProvider.GetRequiredService<IAdaptiveTtlCache>();
 var adaptiveMetrics = await BenchmarkCache(
 adaptiveCache,
 numKeys,
 numOperations,
 hotKeyCount,
 useAdaptive: true);

 // Compare results
 _logger.LogInformation("\nBenchmark Results:");
 _logger.LogInformation("Fixed TTL Cache:");
 DisplayBenchmarkMetrics(fixedMetrics);

 _logger.LogInformation("\nAdaptive TTL Cache:");
 DisplayBenchmarkMetrics(adaptiveMetrics);

 // Calculate improvements
 var hitRateImprovement = (adaptiveMetrics.HitRate - fixedMetrics.HitRate) / fixedMetrics.HitRate;
 var latencyImprovement = (fixedMetrics.AverageLatency - adaptiveMetrics.AverageLatency) / fixedMetrics.AverageLatency;

 _logger.LogInformation("\nImprovements with Adaptive TTL:");
 _logger.LogInformation(" Hit Rate: {Improvement:+0.0%}", hitRateImprovement);
 _logger.LogInformation(" Average Latency: {Improvement:+0.0%}", latencyImprovement);
 _logger.LogInformation(" Cache Efficiency Score: {Score:F2}", adaptiveMetrics.EfficiencyScore);
 }

 #region Helper Methods

 private void ConfigureServices(IServiceCollection services)
 {
 services.AddLogging(builder =>
 {
 builder.AddConsole();
 builder.SetMinimumLevel(LogLevel.Information);
 });

 // Add caching services
 services.AddSingleton<IDistributedCache, InMemoryCacheProvider>();
 services.AddSingleton<ITtlMetricsCollector, DefaultTtlMetricsCollector>();
 services.AddSingleton<IAdaptiveTtlCache, AdaptiveTtlCacheWrapper>();

 // Configure adaptive TTL options
 services.Configure<AdaptiveTtlOptions>(options =>
 {
 options.MinimumTtl = TimeSpan.FromSeconds(30);
 options.MaximumTtl = TimeSpan.FromHours(24);
 options.TargetHitRate = 0.8;
 options.EnableAutoTuning = true;
 });
 }

 private static string GenerateData(string key)
 {
 return $"Data for {key} - Generated at {DateTime.UtcNow:O}";
 }

 private static async Task SimulateTraffic(
 IAdaptiveTtlCache cache,
 string key,
 int requestsPerSecond,
 int delayMs,
 CancellationToken cancellationToken)
 {
 while (!cancellationToken.IsCancellationRequested)
 {
 try
 {
 await cache.GetAsync<string>(key);
 await Task.Delay(delayMs, cancellationToken);
 }
 catch (OperationCanceledException)
 {
 break;
 }
 }
 }

 private CompositeTtlStrategy CreateProductCatalogStrategy(ITtlMetricsCollector metricsCollector)
 {
 var composite = new CompositeTtlStrategy(metricsCollector);

 // Products need high hit rate
 composite.AddStrategy(
 new HitRateBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions { TargetHitRate = 0.9 }),
 metricsCollector),
 0.5);

 // Adjust based on access frequency
 composite.AddStrategy(
 new FrequencyBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector),
 0.5);

 return composite;
 }

 private CompositeTtlStrategy CreateUserSessionStrategy(ITtlMetricsCollector metricsCollector)
 {
 var composite = new CompositeTtlStrategy(metricsCollector);

 // Sessions are time-sensitive
 var timeStrategy = new TimePatternTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector);

 // Shorter TTL during business hours
 timeStrategy.AddPattern(new TimePattern
 {
 Name = "ActiveHours",
 StartHour = 8,
 EndHour = 22,
 TtlMultiplier = 0.5
 });

 composite.AddStrategy(timeStrategy, 0.7);
 composite.AddStrategy(
 new FrequencyBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector),
 0.3);

 return composite;
 }

 private CompositeTtlStrategy CreateInventoryStrategy(ITtlMetricsCollector metricsCollector)
 {
 var composite = new CompositeTtlStrategy(metricsCollector);

 // Inventory needs very short TTL for accuracy
 composite.AddStrategy(
 new FrequencyBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions { MaximumTtl = TimeSpan.FromMinutes(1) }),
 metricsCollector),
 0.8);

 // But can extend slightly under memory pressure
 composite.AddStrategy(
 new MemoryPressureTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector),
 0.2);

 return composite;
 }

 private CompositeTtlStrategy CreateRecommendationStrategy(ITtlMetricsCollector metricsCollector)
 {
 var composite = new CompositeTtlStrategy(metricsCollector);

 // Recommendations can have longer TTL
 composite.AddStrategy(
 new HitRateBasedTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions { TargetHitRate = 0.7 }),
 metricsCollector),
 0.4);

 composite.AddStrategy(
 new TimePatternTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector),
 0.3);

 composite.AddStrategy(
 new MemoryPressureTtlStrategy(
 Microsoft.Extensions.Options.Options.Create(new AdaptiveTtlOptions()),
 metricsCollector),
 0.3);

 return composite;
 }

 private async Task<BenchmarkMetrics> BenchmarkCache(
 object cache,
 int numKeys,
 int numOperations,
 int hotKeyCount,
 bool useAdaptive)
 {
 var keys = Enumerable.Range(1, numKeys).Select(i => $"bench-{i}").ToList();
 var random = new Random(42); // Fixed seed for reproducibility
 var stopwatch = new Stopwatch();

 // Populate cache
 foreach (var key in keys)
 {
 if (useAdaptive && cache is IAdaptiveTtlCache adaptiveCache)
 {
 var strategy = new CompositeTtlStrategy(_serviceProvider.GetRequiredService<ITtlMetricsCollector>());
 await adaptiveCache.SetAsync(key, GenerateData(key), new AdaptiveCacheEntryOptions
 {
 BaseTimeToLive = TimeSpan.FromMinutes(5),
 AdaptiveTtlStrategy = strategy
 });
 }
 else if (cache is IDistributedCache distributedCache)
 {
 await distributedCache.SetStringAsync(key, GenerateData(key), new DistributedCacheEntryOptions
 {
 AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
 });
 }
 }

 // Run benchmark
 var hits = 0;
 var misses = 0;
 var latencies = new List<double>();

 stopwatch.Start();

 for (int i = 0; i < numOperations; i++)
 {
 // 80% of requests go to hot keys (simulating real-world access patterns)
 var keyIndex = random.NextDouble() < 0.8
 ? random.Next(hotKeyCount)
 : random.Next(numKeys);

 var key = keys[keyIndex];

 var opStart = stopwatch.Elapsed;

 string? result = null;
 if (useAdaptive && cache is IAdaptiveTtlCache adaptiveCache)
 {
 result = await adaptiveCache.GetAsync<string>(key);
 }
 else if (cache is IDistributedCache distributedCache)
 {
 result = await distributedCache.GetStringAsync(key);
 }

 var opEnd = stopwatch.Elapsed;
 latencies.Add((opEnd - opStart).TotalMilliseconds);

 if (result != null)
 hits++;
 else
 misses++;

 // Occasionally expire some entries to test adaptation
 if (i % 1000 == 0 && i > 0)
 {
 var expireKey = keys[random.Next(numKeys)];
 if (cache is IDistributedCache distributedCache)
 {
 await distributedCache.RemoveAsync(expireKey);
 }
 }
 }

 stopwatch.Stop();

 return new BenchmarkMetrics
 {
 TotalOperations = numOperations,
 Hits = hits,
 Misses = misses,
 HitRate = (double)hits / numOperations,
 AverageLatency = latencies.Average(),
 P95Latency = latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.95)).First(),
 P99Latency = latencies.OrderBy(l => l).Skip((int)(latencies.Count * 0.99)).First(),
 TotalTime = stopwatch.Elapsed,
 EfficiencyScore = CalculateEfficiencyScore(hits, misses, latencies)
 };
 }

 private double CalculateEfficiencyScore(int hits, int misses, List<double> latencies)
 {
 var hitRate = (double)hits / (hits + misses);
 var avgLatency = latencies.Average();
 var latencyScore = Math.Max(0, 1 - (avgLatency / 100)); // Normalize to 0-1

 // Weighted score: 70% hit rate, 30% latency
 return hitRate * 0.7 + latencyScore * 0.3;
 }

 private void DisplayBenchmarkMetrics(BenchmarkMetrics metrics)
 {
 _logger.LogInformation(" Total Operations: {Operations:N0}", metrics.TotalOperations);
 _logger.LogInformation(" Hits: {Hits:N0} ({HitRate:P2})", metrics.Hits, metrics.HitRate);
 _logger.LogInformation(" Misses: {Misses:N0}", metrics.Misses);
 _logger.LogInformation(" Average Latency: {Latency:F2} ms", metrics.AverageLatency);
 _logger.LogInformation(" P95 Latency: {Latency:F2} ms", metrics.P95Latency);
 _logger.LogInformation(" P99 Latency: {Latency:F2} ms", metrics.P99Latency);
 _logger.LogInformation(" Total Time: {Time:F2} seconds", metrics.TotalTime.TotalSeconds);
 _logger.LogInformation(" Throughput: {Throughput:N0} ops/sec",
 metrics.TotalOperations / metrics.TotalTime.TotalSeconds);
 }

 #endregion

 #region Supporting Types

 private class BenchmarkMetrics {
 public int TotalOperations { get; set; }
 public int Hits { get; set; }
 public int Misses { get; set; }
 public double HitRate { get; set; }
 public double AverageLatency { get; set; }
 public double P95Latency { get; set; }
 public double P99Latency { get; set; }
 public TimeSpan TotalTime { get; set; }
 public double EfficiencyScore { get; set; }
 }

 #endregion
}

/// <summary>
/// Main program entry point.
/// </summary>
public static class Program {
 /// <summary>
 /// Main method.
 /// </summary>
 public static async Task Main(string[] args)
 {
 var example = new AdvancedAdaptiveTtlExample();
 await example.RunAsync();
 }
}
