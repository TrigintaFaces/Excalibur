using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Caching;
using Excalibur.Dispatch.CloudNative.Caching.AdaptiveTtl;
using Excalibur.Dispatch.CloudNative.Caching.Strategies.AdaptiveTTL;
using Excalibur.Dispatch.CloudNative.Caching.Strategies.AdaptiveTTL.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Examples.Caching
{
	/// <summary>
	/// Advanced example demonstrating Adaptive TTL (Time To Live) strategies for intelligent cache expiration.
	/// This example showcases how TTL can be dynamically adjusted based on:
	/// - Access patterns (frequency, recency, burst detection)
	/// - Data volatility (change rate, update patterns)
	/// - System load and memory pressure
	/// - Machine learning predictions
	/// - Business rules and time-based patterns
	/// </summary>
	public class AdvancedAdaptiveTTLExample
	{
		public static async Task Main(string[] args)
		{
			var host = CreateHostBuilder(args).Build();
			var demo = host.Services.GetRequiredService<AdaptiveTTLDemo>();
			await demo.RunDemoAsync();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
		.ConfigureServices((context, services) =>
		{
			// Configure adaptive TTL strategies
			services.AddCloudNativeCaching(options =>
	 {
			options.DefaultProvider = "adaptive-redis";
			options.EnableStatistics = true;
			options.EnableDistributedLocking = true;
		})
	 .AddRedisCache("adaptive-redis", redis =>
	 {
			redis.ConnectionString = "localhost:6379";
			redis.KeyPrefix = "adaptive-ttl-demo:";
		})
	 .AddAdaptiveTTL(ttl =>
	 {
			ttl.Strategy = AdaptiveTTLStrategyType.Hybrid;
			ttl.MinTTL = TimeSpan.FromSeconds(5);
			ttl.MaxTTL = TimeSpan.FromHours(24);
			ttl.DefaultTTL = TimeSpan.FromMinutes(5);

		 // Configure access pattern analysis
			ttl.AccessPatternOptions = new AccessPatternOptions
			{
				WindowSize = TimeSpan.FromMinutes(15),
				MinSampleSize = 10,
				BurstThreshold = 5.0, // 5x normal rate
				EnablePatternRecognition = true
			};

		 // Configure volatility tracking
			ttl.VolatilityOptions = new VolatilityOptions
			{
				TrackingWindow = TimeSpan.FromHours(1),
				ChangeThreshold = 0.1, // 10% change
				EnablePredictiveAnalysis = true
			};

		 // Configure machine learning
			ttl.MachineLearningOptions = new MachineLearningOptions
			{
				ModelType = "GradientBoosting",
				EnableOnlineTraining = true,
				TrainingBatchSize = 100,
				FeatureImportanceThreshold = 0.05
			};
		});

			// Register demo service
			services.AddSingleton<AdaptiveTTLDemo>();
			services.AddHostedService<DataSimulator>();
		});
	}

	/// <summary>
	/// Main demo class showcasing various adaptive TTL strategies
	/// </summary>
	public class AdaptiveTTLDemo
	{
		private readonly ICacheProvider _cache;
		private readonly ILogger<AdaptiveTTLDemo> _logger;
		private readonly IAdaptiveTTLStrategy _adaptiveTTL;
		private readonly ICacheStatistics _statistics;

		public AdaptiveTTLDemo(
		ICacheProvider cache,
		ILogger<AdaptiveTTLDemo> logger,
		IAdaptiveTTLStrategy adaptiveTTL,
		ICacheStatistics statistics)
		{
			_cache = cache;
			_logger = logger;
			_adaptiveTTL = adaptiveTTL;
			_statistics = statistics;
		}

		public async Task RunDemoAsync()
		{
			_logger.LogInformation("=== Advanced Adaptive TTL Demo ===");

			// Demo 1: Access Pattern-Based TTL
			await DemoAccessPatternBasedTTL();

			// Demo 2: Volatility-Based TTL
			await DemoVolatilityBasedTTL();

			// Demo 3: Load-Aware TTL
			await DemoLoadAwareTTL();

			// Demo 4: Machine Learning TTL
			await DemoMachineLearningTTL();

			// Demo 5: Business Rule TTL
			await DemoBusinessRuleTTL();

			// Demo 6: Hybrid Strategy
			await DemoHybridStrategy();

			// Show final statistics
			await ShowStatistics();
		}

		/// <summary>
		/// Demo 1: Adjust TTL based on access patterns
		/// </summary>
		private async Task DemoAccessPatternBasedTTL()
		{
			_logger.LogInformation("\n--- Demo 1: Access Pattern-Based TTL ---");

			// Simulate different access patterns
			var scenarios = new[]
			{
 new { Key = "hot-data", AccessRate = 100, Description = "Frequently accessed data" },
 new { Key = "warm-data", AccessRate = 10, Description = "Moderately accessed data" },
 new { Key = "cold-data", AccessRate = 1, Description = "Rarely accessed data" },
 new { Key = "burst-data", AccessRate = 0, BurstSize = 50, Description = "Burst access pattern" }
 };

			foreach (var scenario in scenarios)
			{
				_logger.LogInformation($"\nTesting: {scenario.Description}");

				// Set initial data
				var data = new CacheableData { Id = scenario.Key, Value = $"Value for {scenario.Key}" };
				var context = new TTLCalculationContext
				{
					Key = scenario.Key,
					DataSize = 1024,
					AccessPattern = new AccessPattern { AccessRate = scenario.AccessRate }
				};

				// Calculate adaptive TTL
				var ttl = await _adaptiveTTL.CalculateTTLAsync(context);
				await _cache.SetAsync(scenario.Key, data, ttl);

				_logger.LogInformation($"Initial TTL: {ttl}");

				// Simulate access pattern
				if (scenario.BurstSize > 0)
				{
					// Burst access
					for (int i = 0; i < scenario.BurstSize; i++)
					{
						await _cache.GetAsync<CacheableData>(scenario.Key);
						await _adaptiveTTL.RecordAccessAsync(scenario.Key);
					}
					_logger.LogInformation($"Burst access completed: {scenario.BurstSize} requests");
				}
				else
				{
					// Regular access pattern
					for (int i = 0; i < scenario.AccessRate; i++)
					{
						await _cache.GetAsync<CacheableData>(scenario.Key);
						await _adaptiveTTL.RecordAccessAsync(scenario.Key);
						await Task.Delay(100); // Spread out accesses
					}
				}

				// Recalculate TTL after access pattern
				context.AccessPattern = await _adaptiveTTL.GetAccessPatternAsync(scenario.Key);
				var newTtl = await _adaptiveTTL.CalculateTTLAsync(context);

				_logger.LogInformation($"Adjusted TTL: {newTtl} (Change: {(newTtl - ttl).TotalSeconds:+0;-0;0}s)");
				_logger.LogInformation($"Access metrics: Rate={context.AccessPattern.AccessRate:F2}/min, " +
				$"Burst={context.AccessPattern.IsBurstDetected}");
			}
		}

		/// <summary>
		/// Demo 2: Adjust TTL based on data volatility
		/// </summary>
		private async Task DemoVolatilityBasedTTL()
		{
			_logger.LogInformation("\n--- Demo 2: Volatility-Based TTL ---");

			var scenarios = new[]
			{
 new { Key = "stable-config", UpdateRate = 0.1, Description = "Stable configuration data" },
 new { Key = "user-preferences", UpdateRate = 1.0, Description = "User preferences" },
 new { Key = "stock-price", UpdateRate = 10.0, Description = "Highly volatile stock data" },
 new { Key = "trending-topics", UpdateRate = 5.0, Pattern = "periodic", Description = "Periodic updates" }
 };

			foreach (var scenario in scenarios)
			{
				_logger.LogInformation($"\nTesting: {scenario.Description}");

				var data = new VolatileData { Id = scenario.Key, Value = 100.0, Timestamp = DateTime.UtcNow };
				var context = new TTLCalculationContext
				{
					Key = scenario.Key,
					DataVolatility = new DataVolatility { ChangeRate = scenario.UpdateRate }
				};

				// Initial TTL
				var ttl = await _adaptiveTTL.CalculateTTLAsync(context);
				await _cache.SetAsync(scenario.Key, data, ttl);
				_logger.LogInformation($"Initial TTL: {ttl}");

				// Simulate updates
				var updateCount = (int)(scenario.UpdateRate * 10);
				for (int i = 0; i < updateCount; i++)
				{
					data.Value += Random.Shared.NextDouble() * 10 - 5; // Random change
					data.Timestamp = DateTime.UtcNow;

					await _cache.SetAsync(scenario.Key, data);
					await _adaptiveTTL.RecordUpdateAsync(scenario.Key, data.Value);

					if (scenario.Pattern == "periodic")
					{
						await Task.Delay(1000 / updateCount); // Regular intervals
					}
					else
					{
						await Task.Delay(Random.Shared.Next(50, 200)); // Random intervals
					}
				}

				// Recalculate TTL based on observed volatility
				context.DataVolatility = await _adaptiveTTL.GetVolatilityAsync(scenario.Key);
				var newTtl = await _adaptiveTTL.CalculateTTLAsync(context);

				_logger.LogInformation($"Adjusted TTL: {newTtl} (Change: {(newTtl - ttl).TotalSeconds:+0;-0;0}s)");
				_logger.LogInformation($"Volatility metrics: ChangeRate={context.DataVolatility.ChangeRate:F2}, " +
				$"StdDev={context.DataVolatility.StandardDeviation:F2}");
			}
		}

		/// <summary>
		/// Demo 3: Adjust TTL based on system load and memory pressure
		/// </summary>
		private async Task DemoLoadAwareTTL()
		{
			_logger.LogInformation("\n--- Demo 3: Load-Aware TTL ---");

			// Simulate different load scenarios
			var loadScenarios = new[]
			{
 new { Load = 0.2, Memory = 0.3, Description = "Low load, plenty of memory" },
 new { Load = 0.5, Memory = 0.5, Description = "Moderate load and memory" },
 new { Load = 0.8, Memory = 0.7, Description = "High load, memory pressure" },
 new { Load = 0.9, Memory = 0.9, Description = "Critical load and memory" }
 };

			foreach (var scenario in loadScenarios)
			{
				_logger.LogInformation($"\nTesting: {scenario.Description}");

				// Simulate system metrics
				var systemMetrics = new SystemMetrics
				{
					CpuUsage = scenario.Load,
					MemoryUsage = scenario.Memory,
					CacheHitRate = 1.0 - scenario.Load * 0.5, // Hit rate decreases with load
					QueueDepth = (int)(scenario.Load * 100)
				};

				// Test with different data priorities
				var priorities = new[] { "critical", "normal", "low" };

				foreach (var priority in priorities)
				{
					var context = new TTLCalculationContext
					{
						Key = $"{priority}-priority-data",
						Priority = GetPriorityValue(priority),
						SystemMetrics = systemMetrics
					};

					var ttl = await _adaptiveTTL.CalculateTTLAsync(context);
					_logger.LogInformation($" {priority} priority data: TTL = {ttl}");
				}
			}
		}

		/// <summary>
		/// Demo 4: Machine Learning-based TTL prediction
		/// </summary>
		private async Task DemoMachineLearningTTL()
		{
			_logger.LogInformation("\n--- Demo 4: Machine Learning TTL ---");

			// Train the model with historical data
			await TrainMLModel();

			// Test predictions for different scenarios
			var testScenarios = new[]
			{
 new
 {
 Description = "E-commerce product during sale",
 Features = new TTLFeatures
 {
 AccessFrequency = 150.0,
 LastAccessRecency = 0.1,
 UpdateFrequency = 5.0,
 DataSize = 2048,
 TimeOfDay = 14, // 2 PM
 DayOfWeek = 5, // Friday
 IsWeekend = false,
 HistoricalHitRate = 0.85,
 PredictedDemand = 0.9
 }
 },
 new
 {
 Description = "User session data at night",
 Features = new TTLFeatures
 {
 AccessFrequency = 10.0,
 LastAccessRecency = 0.8,
 UpdateFrequency = 2.0,
 DataSize = 512,
 TimeOfDay = 2, // 2 AM
 DayOfWeek = 2, // Tuesday
 IsWeekend = false,
 HistoricalHitRate = 0.6,
 PredictedDemand = 0.2
 }
 },
 new
 {
 Description = "Static content on weekend",
 Features = new TTLFeatures
 {
 AccessFrequency = 30.0,
 LastAccessRecency = 0.3,
 UpdateFrequency = 0.1,
 DataSize = 10240,
 TimeOfDay = 10, // 10 AM
 DayOfWeek = 6, // Saturday
 IsWeekend = true,
 HistoricalHitRate = 0.95,
 PredictedDemand = 0.7
 }
 }
 };

			foreach (var scenario in testScenarios)
			{
				_logger.LogInformation($"\nTesting: {scenario.Description}");

				var mlContext = new TTLCalculationContext
				{
					Key = $"ml-test-{Guid.NewGuid():N}",
					Features = scenario.Features
				};

				// Get ML prediction
				var predictedTtl = await _adaptiveTTL.CalculateTTLAsync(mlContext);
				_logger.LogInformation($"ML Predicted TTL: {predictedTtl}");

				// Show feature importance
				var importance = await _adaptiveTTL.GetFeatureImportanceAsync();
				_logger.LogInformation("Top influential features:");
				foreach (var feature in importance.Take(5))
				{
					_logger.LogInformation($" - {feature.Feature}: {feature.Importance:P}");
				}
			}
		}

		/// <summary>
		/// Demo 5: Business rule-based TTL
		/// </summary>
		private async Task DemoBusinessRuleTTL()
		{
			_logger.LogInformation("\n--- Demo 5: Business Rule TTL ---");

			// Define business rules
			var businessRules = new List<BusinessRule>
 {
 new TimeBasedRule
 {
 Name = "Peak Hours Rule",
 Description = "Extend TTL during peak hours",
 StartHour = 9,
 EndHour = 17,
 TTLMultiplier = 2.0
 },
 new DataTypeRule
 {
 Name = "Financial Data Rule",
 Description = "Short TTL for financial data",
 DataTypePattern = "stock|price|rate",
 MaxTTL = TimeSpan.FromMinutes(1)
 },
 new SizeBasedRule
 {
 Name = "Large Object Rule",
 Description = "Longer TTL for large objects",
 SizeThreshold = 1024 * 1024, // 1MB
 MinTTL = TimeSpan.FromHours(1)
 },
 new ComplianceRule
 {
 Name = "GDPR Compliance",
 Description = "Personal data retention limits",
 DataCategory = "personal",
 MaxRetention = TimeSpan.FromDays(30)
 }
 };

			// Test each rule
			foreach (var rule in businessRules)
			{
				_logger.LogInformation($"\nTesting rule: {rule.Name}");
				_logger.LogInformation($"Description: {rule.Description}");

				var testData = GenerateTestDataForRule(rule);
				var context = new TTLCalculationContext
				{
					Key = testData.Key,
					DataType = testData.Type,
					DataSize = testData.Size,
					BusinessRules = new[] { rule },
					Metadata = testData.Metadata
				};

				var ttl = await _adaptiveTTL.CalculateTTLAsync(context);
				_logger.LogInformation($"Calculated TTL: {ttl}");
				_logger.LogInformation($"Rule applied: {rule.IsApplicable(context)}");
			}
		}

		/// <summary>
		/// Demo 6: Hybrid strategy combining all approaches
		/// </summary>
		private async Task DemoHybridStrategy()
		{
			_logger.LogInformation("\n--- Demo 6: Hybrid Strategy ---");

			// Create a complex scenario combining all factors
			var hybridScenario = new
			{
				Key = "hybrid-test-data",
				Description = "E-commerce product page during flash sale",
				InitialValue = new ProductData
				{
					Id = "PROD-12345",
					Name = "Popular Widget",
					Price = 99.99m,
					Stock = 1000,
					Views = 0
				}
			};

			_logger.LogInformation($"Scenario: {hybridScenario.Description}");

			// Set initial data
			var ttlContext = new TTLCalculationContext
			{
				Key = hybridScenario.Key,
				DataType = "product",
				Priority = CachePriority.High,
				Features = new TTLFeatures
				{
					TimeOfDay = DateTime.Excalibur.Data.Hour,
					DayOfWeek = (int)DateTime.Excalibur.Data.DayOfWeek,
					IsWeekend = DateTime.Excalibur.Data.DayOfWeek >= DayOfWeek.Saturday
				}
			};

			var initialTtl = await _adaptiveTTL.CalculateTTLAsync(ttlContext);
			await _cache.SetAsync(hybridScenario.Key, hybridScenario.InitialValue, initialTtl);
			_logger.LogInformation($"Initial TTL: {initialTtl}");

			// Simulate flash sale activity
			_logger.LogInformation("\nSimulating flash sale activity...");

			var tasks = new List<Task>();
			var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

			// Simulate concurrent users
			for (int user = 0; user < 5; user++)
			{
				tasks.Add(SimulateUserActivity(hybridScenario.Key, user, cts.Token));
			}

			// Simulate inventory updates
			tasks.Add(SimulateInventoryUpdates(hybridScenario.Key, cts.Token));

			// Monitor and adjust TTL
			tasks.Add(MonitorAndAdjustTTL(hybridScenario.Key, cts.Token));

			await Task.WhenAll(tasks);

			// Show final metrics
			var finalPattern = await _adaptiveTTL.GetAccessPatternAsync(hybridScenario.Key);
			var finalVolatility = await _adaptiveTTL.GetVolatilityAsync(hybridScenario.Key);
			var metrics = await _adaptiveTTL.GetMetricsAsync(hybridScenario.Key);

			_logger.LogInformation("\nFinal metrics:");
			_logger.LogInformation($"Access rate: {finalPattern.AccessRate:F2} req/min");
			_logger.LogInformation($"Volatility: {finalVolatility.ChangeRate:F2} changes/min");
			_logger.LogInformation($"TTL adjustments: {metrics.TTLAdjustmentCount}");
			_logger.LogInformation($"Average TTL: {metrics.AverageTTL}");
			_logger.LogInformation($"Cache efficiency: {metrics.CacheEfficiency:P}");
		}

		// Helper methods

		private async Task TrainMLModel()
		{
			_logger.LogInformation("Training ML model with historical data...");

			// Generate training data
			var trainingData = new List<TTLTrainingExample>();

			for (int i = 0; i < 1000; i++)
			{
				var example = new TTLTrainingExample
				{
					Features = GenerateRandomFeatures(),
					OptimalTTL = CalculateOptimalTTL(GenerateRandomFeatures()),
					Outcome = new TTLOutcome
					{
						HitRate = Random.Shared.NextDouble(),
						StalenessRate = Random.Shared.NextDouble() * 0.1,
						MemoryEfficiency = Random.Shared.NextDouble()
					}
				};
				trainingData.Add(example);
			}

			// Train the model
			await _adaptiveTTL.TrainModelAsync(trainingData);
			_logger.LogInformation($"Model trained with {trainingData.Count} examples");
		}

		private TTLFeatures GenerateRandomFeatures()
		{
			return new TTLFeatures
			{
				AccessFrequency = Random.Shared.NextDouble() * 200,
				LastAccessRecency = Random.Shared.NextDouble(),
				UpdateFrequency = Random.Shared.NextDouble() * 50,
				DataSize = Random.Shared.Next(100, 100000),
				TimeOfDay = Random.Shared.Next(0, 24),
				DayOfWeek = Random.Shared.Next(0, 7),
				IsWeekend = Random.Shared.Next(0, 2) == 1,
				HistoricalHitRate = Random.Shared.NextDouble(),
				PredictedDemand = Random.Shared.NextDouble()
			};
		}

		private TimeSpan CalculateOptimalTTL(TTLFeatures features)
		{
			// Simple heuristic for demonstration
			var baseMinutes = 60.0;
			baseMinutes *= (1 + features.HistoricalHitRate);
			baseMinutes /= (1 + features.UpdateFrequency * 0.1);
			baseMinutes *= (1 + features.AccessFrequency * 0.01);

			return TimeSpan.FromMinutes(Math.Max(1, Math.Min(1440, baseMinutes)));
		}

		private async Task SimulateUserActivity(string key, int userId, CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				await _cache.GetAsync<ProductData>(key);
				await _adaptiveTTL.RecordAccessAsync(key, new AccessMetadata
				{
					UserId = $"user-{userId}",
					SessionId = Guid.NewGuid().ToString(),
					DeviceType = userId % 2 == 0 ? "mobile" : "desktop"
				});

				var delay = Random.Shared.Next(100, 500);
				await Task.Delay(delay, ct);
			}
		}

		private async Task SimulateInventoryUpdates(string key, CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				var product = await _cache.GetAsync<ProductData>(key);
				if (product != null)
				{
					product.Stock -= Random.Shared.Next(1, 10);
					product.Views += Random.Shared.Next(5, 20);

					await _cache.SetAsync(key, product);
					await _adaptiveTTL.RecordUpdateAsync(key, product);
				}

				await Task.Delay(Random.Shared.Next(500, 1500), ct);
			}
		}

		private async Task MonitorAndAdjustTTL(string key, CancellationToken ct)
		{
			var adjustmentCount = 0;

			while (!ct.IsCancellationRequested)
			{
				await Task.Delay(1000, ct);

				// Get current metrics
				var pattern = await _adaptiveTTL.GetAccessPatternAsync(key);
				var volatility = await _adaptiveTTL.GetVolatilityAsync(key);

				// Recalculate TTL if needed
				var context = new TTLCalculationContext
				{
					Key = key,
					AccessPattern = pattern,
					DataVolatility = volatility,
					SystemMetrics = GetCurrentSystemMetrics()
				};

				var newTtl = await _adaptiveTTL.CalculateTTLAsync(context);

				// Apply new TTL if significantly different
				var currentTtl = await _cache.GetTTLAsync(key);
				if (currentTtl.HasValue && Math.Abs((newTtl - currentTtl.Value).TotalSeconds) > 10)
				{
					await _cache.UpdateTTLAsync(key, newTtl);
					adjustmentCount++;
					_logger.LogInformation($"TTL adjusted: {currentTtl.Value} -> {newTtl} (adjustment #{adjustmentCount})");
				}
			}
		}

		private SystemMetrics GetCurrentSystemMetrics()
		{
			var process = Process.GetCurrentProcess();
			var totalMemory = GC.GetTotalMemory(false);

			return new SystemMetrics
			{
				CpuUsage = process.TotalProcessorTime.TotalSeconds / Environment.ProcessorCount / Environment.TickCount * 100,
				MemoryUsage = (double)totalMemory / (1024 * 1024 * 1024), // GB
				CacheHitRate = _statistics.GetHitRate(),
				QueueDepth = 0 // Would come from actual queue monitoring
			};
		}

		private CachePriority GetPriorityValue(string priority) => priority switch
		{
			"critical" => CachePriority.Critical,
			"normal" => CachePriority.Normal,
			"low" => CachePriority.Low,
			_ => CachePriority.Normal
		};

		private TestData GenerateTestDataForRule(BusinessRule rule)
		{
			return rule switch
			{
				TimeBasedRule => new TestData
				{
					Key = "time-sensitive-data",
					Type = "general",
					Size = 1024
				},
				DataTypeRule => new TestData
				{
					Key = "stock-price-AAPL",
					Type = "stock-price",
					Size = 256
				},
				SizeBasedRule => new TestData
				{
					Key = "large-report",
					Type = "report",
					Size = 2 * 1024 * 1024
				},
				ComplianceRule => new TestData
				{
					Key = "user-profile-12345",
					Type = "user-data",
					Size = 4096,
					Metadata = new Dictionary<string, string> { ["category"] = "personal" }
				},
				_ => new TestData { Key = "default", Type = "general", Size = 1024 }
			};
		}

		private async Task ShowStatistics()
		{
			_logger.LogInformation("\n=== Final Statistics ===");

			var stats = _statistics.GetSnapshot();
			_logger.LogInformation($"Total requests: {stats.TotalRequests:N0}");
			_logger.LogInformation($"Cache hits: {stats.CacheHits:N0} ({stats.HitRate:P})");
			_logger.LogInformation($"Cache misses: {stats.CacheMisses:N0}");
			_logger.LogInformation($"Average latency: {stats.AverageLatency:F2}ms");

			var ttlStats = await _adaptiveTTL.GetGlobalMetricsAsync();
			_logger.LogInformation($"\nTTL adjustments: {ttlStats.TotalAdjustments:N0}");
			_logger.LogInformation($"Average TTL: {ttlStats.AverageTTL}");
			_logger.LogInformation($"TTL reduction rate: {ttlStats.ReductionRate:P}");
			_logger.LogInformation($"TTL extension rate: {ttlStats.ExtensionRate:P}");
			_logger.LogInformation($"Memory saved: {ttlStats.MemorySaved / (1024.0 * 1024.0):F2} MB");
		}
	}

	// Supporting classes

	public class CacheableData
	{
		public string Id { get; set; }
		public string Value { get; set; }
		public DateTime Created { get; set; } = DateTime.UtcNow;
	}

	public class VolatileData : CacheableData
	{
		public double Value { get; set; }
		public DateTime Timestamp { get; set; }
	}

	public class ProductData : CacheableData
	{
		public string Name { get; set; }
		public decimal Price { get; set; }
		public int Stock { get; set; }
		public int Views { get; set; }
	}

	public class AccessMetadata
	{
		public string UserId { get; set; }
		public string SessionId { get; set; }
		public string DeviceType { get; set; }
	}

	public class TestData
	{
		public string Key { get; set; }
		public string Type { get; set; }
		public long Size { get; set; }
		public Dictionary<string, string> Metadata { get; set; }
	}

	public abstract class BusinessRule
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public abstract bool IsApplicable(TTLCalculationContext context);
		public abstract TimeSpan ApplyRule(TimeSpan baseTTL, TTLCalculationContext context);
	}

	public class TimeBasedRule : BusinessRule
	{
		public int StartHour { get; set; }
		public int EndHour { get; set; }
		public double TTLMultiplier { get; set; }

		public override bool IsApplicable(TTLCalculationContext context)
		{
			var hour = DateTime.Excalibur.Data.Hour;
			return hour >= StartHour && hour <= EndHour;
		}

		public override TimeSpan ApplyRule(TimeSpan baseTTL, TTLCalculationContext context)
		{
			return IsApplicable(context)
			? TimeSpan.FromMilliseconds(baseTTL.TotalMilliseconds * TTLMultiplier)
			: baseTTL;
		}
	}

	public class DataTypeRule : BusinessRule
	{
		public string DataTypePattern { get; set; }
		public TimeSpan MaxTTL { get; set; }

		public override bool IsApplicable(TTLCalculationContext context)
		{
			return context.DataType != null &&
			System.Text.RegularExpressions.Regex.IsMatch(context.DataType, DataTypePattern);
		}

		public override TimeSpan ApplyRule(TimeSpan baseTTL, TTLCalculationContext context)
		{
			return IsApplicable(context) ? TimeSpan.FromMilliseconds(Math.Min(baseTTL.TotalMilliseconds, MaxTTL.TotalMilliseconds)) : baseTTL;
		}
	}

	public class SizeBasedRule : BusinessRule
	{
		public long SizeThreshold { get; set; }
		public TimeSpan MinTTL { get; set; }

		public override bool IsApplicable(TTLCalculationContext context)
		{
			return context.DataSize >= SizeThreshold;
		}

		public override TimeSpan ApplyRule(TimeSpan baseTTL, TTLCalculationContext context)
		{
			return IsApplicable(context) ? TimeSpan.FromMilliseconds(Math.Max(baseTTL.TotalMilliseconds, MinTTL.TotalMilliseconds)) : baseTTL;
		}
	}

	public class ComplianceRule : BusinessRule
	{
		public string DataCategory { get; set; }
		public TimeSpan MaxRetention { get; set; }

		public override bool IsApplicable(TTLCalculationContext context)
		{
			return context.Metadata?.TryGetValue("category", out var category) == true &&
			category == DataCategory;
		}

		public override TimeSpan ApplyRule(TimeSpan baseTTL, TTLCalculationContext context)
		{
			return IsApplicable(context) ? TimeSpan.FromMilliseconds(Math.Min(baseTTL.TotalMilliseconds, MaxRetention.TotalMilliseconds)) : baseTTL;
		}
	}

	// Background service to simulate realistic data changes
	public class DataSimulator : BackgroundService
	{
		private readonly ICacheProvider _cache;
		private readonly ILogger<DataSimulator> _logger;

		public DataSimulator(ICacheProvider cache, ILogger<DataSimulator> logger)
		{
			_cache = cache;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Data simulator started");

			while (!stoppingToken.IsCancellationRequested)
			{
				// Simulate various data patterns
				await SimulateStockPrices(stoppingToken);
				await SimulateUserSessions(stoppingToken);
				await SimulateConfigChanges(stoppingToken);

				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}
		}

		private async Task SimulateStockPrices(CancellationToken ct)
		{
			var stocks = new[] { "AAPL", "GOOGL", "MSFT", "AMZN" };

			foreach (var stock in stocks)
			{
				var key = $"stock-price-{stock}";
				var price = 100 + Random.Shared.NextDouble() * 50;
				await _cache.SetAsync(key, new { Symbol = stock, Price = price, Time = DateTime.UtcNow });
			}
		}

		private async Task SimulateUserSessions(CancellationToken ct)
		{
			for (int i = 0; i < Random.Shared.Next(1, 5); i++)
			{
				var sessionId = Guid.NewGuid().ToString();
				var key = $"session-{sessionId}";
				await _cache.SetAsync(key, new { SessionId = sessionId, UserId = $"user-{Random.Shared.Next(1000)}", LastActivity = DateTime.UtcNow });
			}
		}

		private async Task SimulateConfigChanges(CancellationToken ct)
		{
			if (Random.Shared.NextDouble() < 0.1) // 10% chance
			{
				var key = "app-config";
				await _cache.SetAsync(key, new { Version = DateTime.UtcNow.Ticks, FeatureFlags = GenerateFeatureFlags() });
			}
		}

		private Dictionary<string, bool> GenerateFeatureFlags()
		{
			return new Dictionary<string, bool>
			{
				["feature-a"] = Random.Shared.NextDouble() > 0.5,
				["feature-b"] = Random.Shared.NextDouble() > 0.5,
				["feature-c"] = Random.Shared.NextDouble() > 0.5
			};
		}
	}
}
