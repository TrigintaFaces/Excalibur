// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Excalibur.Dispatch.Common;

namespace examples.Consolidated
 /// <summary>
 /// Unified cache circuit breaker example consolidating patterns from multiple implementations.
 /// Demonstrates advanced circuit breaker patterns for resilient cache operations.
 /// </summary>
 public class UnifiedCacheCircuitBreakerExample
{
	private static readonly Random _random = new();

	public static async Task Main(string[] args)
	{
		var host = CreateHostBuilder(args).Build();

		Console.WriteLine("=== Unified Cache Circuit Breaker Example ===\n");
		Console.WriteLine("This example consolidates patterns from:");
		Console.WriteLine("- Excalibur.Dispatch.CloudNative.Patterns");
		Console.WriteLine("- Excalibur.Dispatch.Transport.Patterns");
		Console.WriteLine("- CircuitBreakerCache examples\n");

		// Run comprehensive scenarios
		await RunBasicCircuitBreakerScenario(host.Services);
		await RunMultiRegionScenario(host.Services);
		await RunCascadingFailurePreventionScenario(host.Services);
		await RunAdaptiveTtlWithCircuitBreakerScenario(host.Services);
		await RunPerformanceBenchmark(host.Services);

		Console.WriteLine("\n=== Example Complete ===");
	}

	private static IHostBuilder CreateHostBuilder(string[] args)
	{
		return Host.CreateDefaultBuilder(args)
		.ConfigureServices((context, services) =>
		{
			// Configure logging
			services.AddLogging(builder =>
	 {
		 builder.AddConsole();
		 builder.SetMinimumLevel(LogLevel.Information);
	 });

			// Add caching services
			services.AddMemoryCache();
			services.AddSingleton<IDistributedCache, MemoryDistributedCache>();

			// Add circuit breaker services
			services.AddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();
			services.Configure<CircuitBreakerOptions>(options =>
	 {
		 options.FailureThreshold = 5;
		 options.SuccessThreshold = 3;
		 options.OpenDuration = TimeSpan.FromSeconds(10);
		 options.OperationTimeout = TimeSpan.FromSeconds(2);
	 });

			// Add example services
			services.AddSingleton<SimulatedCacheService>();
			services.AddSingleton<DataService>();
			services.AddSingleton<RegionalCacheService>();
		});
	}

	#region Basic Circuit Breaker Scenario

	private static async Task RunBasicCircuitBreakerScenario(IServiceProvider provider)
	{
		Console.WriteLine("\n1. Basic Circuit Breaker Scenario");
		Console.WriteLine("=================================");

		var dataService = provider.GetRequiredService<DataService>();
		var logger = provider.GetRequiredService<ILogger<UnifiedCacheCircuitBreakerExample>>();

		logger.LogInformation("Testing basic circuit breaker functionality...");

		// Test normal operation
		logger.LogInformation("Testing normal operations...");
		for (int i = 0; i < 5; i++)
		{
			var result = await dataService.GetDataAsync($"key-{i}");
			logger.LogInformation("Operation {Index}: {Result}", i + 1, result.IsSuccess ? "SUCCESS" : "FAILED");
		}

		// Simulate failures to open circuit
		logger.LogInformation("\nSimulating failures to open circuit...");
		dataService.SimulateFailures(true);

		for (int i = 0; i < 8; i++)
		{
			var result = await dataService.GetDataAsync($"failing-key-{i}");
			logger.LogInformation("Failure test {Index}: {Result} (State: {State})",
			i + 1,
			result.IsSuccess ? "SUCCESS" : result.IsFromFallback ? "FALLBACK" : "FAILED",
			result.CircuitState);
		}

		// Reset and test recovery
		logger.LogInformation("\nTesting circuit recovery...");
		dataService.SimulateFailures(false);

		await Task.Delay(TimeSpan.FromSeconds(12)); // Wait for circuit to attempt recovery

		for (int i = 0; i < 5; i++)
		{
			var result = await dataService.GetDataAsync($"recovery-key-{i}");
			logger.LogInformation("Recovery test {Index}: {Result} (State: {State})",
			i + 1,
			result.IsSuccess ? "SUCCESS" : "FAILED",
			result.CircuitState);
		}
	}

	#endregion

	#region Multi-Region Scenario

	private static async Task RunMultiRegionScenario(IServiceProvider provider)
	{
		Console.WriteLine("\n2. Multi-Region Circuit Breaker Scenario");
		Console.WriteLine("========================================");

		var regionalService = provider.GetRequiredService<RegionalCacheService>();
		var logger = provider.GetRequiredService<ILogger<UnifiedCacheCircuitBreakerExample>>();

		var regions = new[] { "us-east-1", "us-west-2", "eu-central-1" };
		var tasks = new List<Task>();

		logger.LogInformation("Testing regional isolation...");

		// Simulate different failure patterns per region
		foreach (var region in regions)
		{
			tasks.Add(SimulateRegionalLoad(regionalService, region, logger));
		}

		await Task.WhenAll(tasks);

		// Display regional metrics
		var metrics = regionalService.GetAllRegionalMetrics();
		foreach (var (region, metric) in metrics)
		{
			logger.LogInformation("Region {Region}: State={State}, Success Rate={SuccessRate:P2}, Total Requests={TotalRequests}",
			region, metric.CurrentState, metric.SuccessRate, metric.TotalRequests);
		}
	}

	private static async Task SimulateRegionalLoad(RegionalCacheService service, string region, ILogger logger)
	{
		// Different failure rates per region
		var failureRate = region switch
		{
			"us-east-1" => 0.1, // 10% failure
			"us-west-2" => 0.4, // 40% failure
			"eu-central-1" => 0.7, // 70% failure
			_ => 0.2
		};

		service.SetRegionalFailureRate(region, failureRate);

		for (int i = 0; i < 20; i++)
		{
			var result = await service.GetDataFromRegionAsync(region, $"regional-key-{i}");
			if (i % 5 == 0) // Log every 5th operation
			{
				logger.LogInformation("{Region}: Operation {Index} - {Result}",
				region, i + 1, result.IsSuccess ? "SUCCESS" : result.IsFromFallback ? "FALLBACK" : "FAILED");
			}

			await Task.Delay(50); // Small delay between operations
		}
	}

	#endregion

	#region Cascading Failure Prevention

	private static async Task RunCascadingFailurePreventionScenario(IServiceProvider provider)
	{
		Console.WriteLine("\n3. Cascading Failure Prevention Scenario");
		Console.WriteLine("=======================================");

		var dataService = provider.GetRequiredService<DataService>();
		var logger = provider.GetRequiredService<ILogger<UnifiedCacheCircuitBreakerExample>>();

		logger.LogInformation("Testing cascading failure prevention...");

		// Simulate high load with cascading failures
		dataService.SimulateFailures(true);

		var tasks = new List<Task>();
		var semaphore = new SemaphoreSlim(50); // Limit concurrent operations

		for (int i = 0; i < 200; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				await semaphore.WaitAsync();
				try
				{
					var result = await dataService.GetDataAsync($"cascade-test-{_random.Next(1000)}");
					// Operations that would normally cascade are now handled by circuit breaker
				}
				finally
				{
					semaphore.Release();
				}
			}));
		}

		var stopwatch = Stopwatch.StartNew();
		await Task.WhenAll(tasks);
		stopwatch.Stop();

		var metrics = dataService.GetMetrics();
		logger.LogInformation("Cascading failure test completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
		logger.LogInformation("Total requests: {Total}, Successes: {Success}, Fallbacks: {Fallback}, Rejected: {Rejected}",
		metrics.TotalRequests, metrics.SuccessfulRequests, metrics.FallbackExecutions, metrics.RejectedRequests);
		logger.LogInformation("Circuit prevented potential cascading failures by rejecting {Rejected} requests",
		metrics.RejectedRequests);
	}

	#endregion

	#region Adaptive TTL with Circuit Breaker

	private static async Task RunAdaptiveTtlWithCircuitBreakerScenario(IServiceProvider provider)
	{
		Console.WriteLine("\n4. Adaptive TTL with Circuit Breaker Scenario");
		Console.WriteLine("============================================");

		var logger = provider.GetRequiredService<ILogger<UnifiedCacheCircuitBreakerExample>>();
		var cache = provider.GetRequiredService<IDistributedCache>();

		logger.LogInformation("Demonstrating adaptive TTL based on circuit breaker state...");

		// Create adaptive cache wrapper that adjusts TTL based on circuit health
		var adaptiveCacheService = new AdaptiveTtlCacheService(
		cache,
		provider.GetRequiredService<ICircuitBreakerFactory>(),
		logger);

		// Test different scenarios
		var scenarios = new[]
		{
 ("healthy-key", false, "Healthy cache operations"),
 ("degraded-key", true, "Degraded cache operations")
 };

		foreach (var (key, simulateFailure, description) in scenarios)
		{
			logger.LogInformation("\nTesting: {Description}", description);

			adaptiveCacheService.SimulateFailures(simulateFailure);

			for (int i = 0; i < 10; i++)
			{
				var result = await adaptiveCacheService.GetWithAdaptiveTtlAsync($"{key}-{i}", () =>
				Task.FromResult($"Data for {key}-{i} generated at {DateTime.UtcNow:HH:mm:ss}"));

				if (i % 3 == 0)
				{
					logger.LogInformation("Key: {Key}, TTL: {Ttl}s, Source: {Source}",
					$"{key}-{i}",
					result.EffectiveTtl.TotalSeconds,
					result.IsFromCache ? "Cache" : "Source");
				}
			}
		}
	}

	#endregion

	#region Performance Benchmark

	private static async Task RunPerformanceBenchmark(IServiceProvider provider)
	{
		Console.WriteLine("\n5. Performance Benchmark");
		Console.WriteLine("========================");

		var logger = provider.GetRequiredService<ILogger<UnifiedCacheCircuitBreakerExample>>();

		const int operationCount = 10000;
		const double failureRate = 0.3;

		logger.LogInformation("Running performance benchmark with {Operations} operations at {FailureRate:P0} failure rate...",
		operationCount, failureRate);

		// Benchmark without circuit breaker
		var withoutCbTime = await BenchmarkWithoutCircuitBreaker(operationCount, failureRate, logger);

		// Benchmark with circuit breaker
		var dataService = provider.GetRequiredService<DataService>();
		dataService.SimulateFailures(true);

		var withCbTime = await BenchmarkWithCircuitBreaker(dataService, operationCount, logger);

		// Calculate improvement
		var improvement = (withoutCbTime.TotalMilliseconds - withCbTime.TotalMilliseconds) / withoutCbTime.TotalMilliseconds;

		logger.LogInformation("\nBenchmark Results:");
		logger.LogInformation("Without Circuit Breaker: {Time:F2}ms", withoutCbTime.TotalMilliseconds);
		logger.LogInformation("With Circuit Breaker: {Time:F2}ms", withCbTime.TotalMilliseconds);
		logger.LogInformation("Performance Improvement: {Improvement:P2}", improvement);

		if (improvement > 0)
		{
			logger.LogInformation("✓ Circuit breaker improved performance by preventing slow failures");
		}
		else
		{
			logger.LogInformation("→ Circuit breaker added small overhead but provided failure protection");
		}
	}

	private static async Task<TimeSpan> BenchmarkWithoutCircuitBreaker(int operationCount, double failureRate, ILogger logger)
	{
		var stopwatch = Stopwatch.StartNew();

		for (int i = 0; i < operationCount; i++)
		{
			try
			{
				if (_random.NextDouble() < failureRate)
				{
					// Simulate slow failure
					await Task.Delay(100);
					throw new InvalidOperationException("Simulated failure");
				}

				// Simulate successful operation
				await Task.Delay(1);
			}
			catch
			{
				// Handle failure (would normally cascade)
				await Task.Delay(10); // Simulate fallback processing
			}
		}

		stopwatch.Stop();
		return stopwatch.Elapsed;
	}

	private static async Task<TimeSpan> BenchmarkWithCircuitBreaker(DataService dataService, int operationCount, ILogger logger)
	{
		var stopwatch = Stopwatch.StartNew();

		for (int i = 0; i < operationCount; i++)
		{
			await dataService.GetDataAsync($"benchmark-key-{i}");
		}

		stopwatch.Stop();
		return stopwatch.Elapsed;
	}

	#endregion
}

#region Supporting Services

/// <summary>
/// Circuit breaker options for configuration
/// </summary>
public class CircuitBreakerOptions
{
	public int FailureThreshold { get; set; } = 5;
	public int SuccessThreshold { get; set; } = 3;
	public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);
	public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitState
{
	Closed,
	Open,
	HalfOpen
}

/// <summary>
/// Interface for circuit breaker factory
/// </summary>
public interface ICircuitBreakerFactory
{
	ICircuitBreaker GetOrCreate(string name);
	Dictionary<string, CircuitBreakerMetrics> GetAllMetrics();
}

/// <summary>
/// Interface for circuit breaker
/// </summary>
public interface ICircuitBreaker
{
	Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback, CancellationToken cancellationToken = default);
	CircuitState GetState();
	CircuitBreakerMetrics GetMetrics();
}

/// <summary>
/// Circuit breaker metrics
/// </summary>
public class CircuitBreakerMetrics
{
	public long TotalRequests { get; set; }
	public long SuccessfulRequests { get; set; }
	public long FailedRequests { get; set; }
	public long RejectedRequests { get; set; }
	public long FallbackExecutions { get; set; }
	public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
	public CircuitState CurrentState { get; set; }
	public TimeSpan AverageResponseTime { get; set; }
}

/// <summary>
/// Operation result with circuit breaker context
/// </summary>
public class OperationResult<T>
{
	public T? Value { get; set; }
	public bool IsSuccess { get; set; }
	public bool IsFromFallback { get; set; }
	public CircuitState CircuitState { get; set; }
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// Adaptive cache result
/// </summary>
public class AdaptiveCacheResult<T>
{
	public T Value { get; set; } = default!;
	public TimeSpan EffectiveTtl { get; set; }
	public bool IsFromCache { get; set; }
}

/// <summary>
/// Implementation of circuit breaker factory
/// </summary>
public class CircuitBreakerFactory : ICircuitBreakerFactory
{
	private readonly ConcurrentDictionary<string, ICircuitBreaker> _circuitBreakers = new();
	private readonly CircuitBreakerOptions _options;
	private readonly ILogger<CircuitBreakerFactory> _logger;

	public CircuitBreakerFactory(IOptions<CircuitBreakerOptions> options, ILogger<CircuitBreakerFactory> logger)
	{
		_options = options.Value;
		_logger = logger;
	}

	public ICircuitBreaker GetOrCreate(string name)
	{
		return _circuitBreakers.GetOrAdd(name, n => new CircuitBreaker(n, _options, _logger));
	}

	public Dictionary<string, CircuitBreakerMetrics> GetAllMetrics()
	{
		return _circuitBreakers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetMetrics());
	}
}

/// <summary>
/// Implementation of circuit breaker
/// </summary>
public class CircuitBreaker : ICircuitBreaker
{
	private readonly string _name;
	private readonly CircuitBreakerOptions _options;
	private readonly ILogger _logger;
	private readonly SemaphoreSlim _halfOpenSemaphore;

	private CircuitState _state = CircuitState.Closed;
	private int _consecutiveFailures = 0;
	private int _consecutiveSuccesses = 0;
	private DateTime _openedAt = DateTime.MinValue;
	private readonly CircuitBreakerMetrics _metrics = new();
	private readonly object _stateLock = new object();

	public CircuitBreaker(string name, CircuitBreakerOptions options, ILogger logger)
	{
		_name = name;
		_options = options;
		_logger = logger;
		_halfOpenSemaphore = new SemaphoreSlim(1, 1);
	}

	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback, CancellationToken cancellationToken = default)
	{
		Interlocked.Increment(ref _metrics.TotalRequests);
		var stopwatch = Stopwatch.StartNew();

		try
		{
			if (!ShouldAllowRequest())
			{
				Interlocked.Increment(ref _metrics.RejectedRequests);
				Interlocked.Increment(ref _metrics.FallbackExecutions);
				return await fallback().ConfigureAwait(false);
			}

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(_options.OperationTimeout);

			var result = await operation().ConfigureAwait(false);
			RecordSuccess();
			Interlocked.Increment(ref _metrics.SuccessfulRequests);
			return result;
		}
		catch (Exception)
		{
			RecordFailure();
			Interlocked.Increment(ref _metrics.FailedRequests);
			Interlocked.Increment(ref _metrics.FallbackExecutions);
			return await fallback().ConfigureAwait(false);
		}
		finally
		{
			stopwatch.Stop();
			var avgTime = TimeSpan.FromMilliseconds(
			(_metrics.AverageResponseTime.TotalMilliseconds + stopwatch.Elapsed.TotalMilliseconds) / 2);
			_metrics.AverageResponseTime = avgTime;
		}
	}

	private bool ShouldAllowRequest()
	{
		lock (_stateLock)
		{
			return _state switch
			{
				CircuitState.Closed => true,
				CircuitState.Open => DateTime.UtcNow - _openedAt >= _options.OpenDuration && TransitionToHalfOpen(),
				CircuitState.HalfOpen => _halfOpenSemaphore.Wait(0),
				_ => false
			};
		}
	}

	private void RecordSuccess()
	{
		lock (_stateLock)
		{
			_consecutiveFailures = 0;
			_consecutiveSuccesses++;

			if (_state == CircuitState.HalfOpen && _consecutiveSuccesses >= _options.SuccessThreshold)
			{
				TransitionToClosed();
			}
		}
	}

	private void RecordFailure()
	{
		lock (_stateLock)
		{
			_consecutiveSuccesses = 0;
			_consecutiveFailures++;

			if ((_state == CircuitState.Closed && _consecutiveFailures >= _options.FailureThreshold) ||
			_state == CircuitState.HalfOpen)
			{
				TransitionToOpen();
			}
		}
	}

	private bool TransitionToHalfOpen()
	{
		_state = CircuitState.HalfOpen;
		_consecutiveSuccesses = 0;
		_consecutiveFailures = 0;
		_metrics.CurrentState = _state;
		_logger.LogInformation("Circuit breaker {Name} transitioned to HALF-OPEN", _name);
		return true;
	}

	private void TransitionToClosed()
	{
		_state = CircuitState.Closed;
		_consecutiveFailures = 0;
		_metrics.CurrentState = _state;
		if (_halfOpenSemaphore.CurrentCount == 0)
			_halfOpenSemaphore.Release();
		_logger.LogInformation("Circuit breaker {Name} transitioned to CLOSED", _name);
	}

	private void TransitionToOpen()
	{
		_state = CircuitState.Open;
		_openedAt = DateTime.UtcNow;
		_metrics.CurrentState = _state;
		_logger.LogWarning("Circuit breaker {Name} transitioned to OPEN", _name);
	}

	public CircuitState GetState() => _state;
	public CircuitBreakerMetrics GetMetrics() => _metrics;
}

/// <summary>
/// Simulated cache service for testing
/// </summary>
public class SimulatedCacheService
{
	private readonly IMemoryCache _cache;
	private readonly ILogger<SimulatedCacheService> _logger;
	private volatile bool _simulateFailures = false;

	public SimulatedCacheService(IMemoryCache cache, ILogger<SimulatedCacheService> logger)
	{
		_cache = cache;
		_logger = logger;
	}

	public void SimulateFailures(bool enable) => _simulateFailures = enable;

	public async Task<string> GetDataAsync(string key)
	{
		if (_simulateFailures && _random.NextDouble() < 0.7) // 70% failure rate when enabled
		{
			await Task.Delay(100); // Simulate slow failure
			throw new InvalidOperationException($"Simulated failure for key: {key}");
		}

		// Check cache first
		if (_cache.TryGetValue(key, out string cachedValue))
		{
			return cachedValue;
		}

		// Simulate data retrieval
		await Task.Delay(_random.Next(10, 50));
		var value = $"Data for {key} at {DateTime.UtcNow:HH:mm:ss}";

		_cache.Set(key, value, TimeSpan.FromMinutes(5));
		return value;
	}
}

/// <summary>
/// Data service with circuit breaker protection
/// </summary>
public class DataService
{
	private readonly SimulatedCacheService _cacheService;
	private readonly ICircuitBreaker _circuitBreaker;
	private readonly ILogger<DataService> _logger;

	public DataService(SimulatedCacheService cacheService, ICircuitBreakerFactory circuitBreakerFactory, ILogger<DataService> logger)
	{
		_cacheService = cacheService;
		_circuitBreaker = circuitBreakerFactory.GetOrCreate("DataService");
		_logger = logger;
	}

	public void SimulateFailures(bool enable) => _cacheService.SimulateFailures(enable);

	public async Task<OperationResult<string>> GetDataAsync(string key)
	{
		try
		{
			var value = await _circuitBreaker.ExecuteAsync(
			async () => await _cacheService.GetDataAsync(key),
			async () =>
			{
				await Task.Delay(5); // Fast fallback
				return $"Fallback data for {key}";
			});

			return new OperationResult<string>
			{
				Value = value,
				IsSuccess = !value.StartsWith("Fallback"),
				IsFromFallback = value.StartsWith("Fallback"),
				CircuitState = _circuitBreaker.GetState()
			};
		}
		catch (Exception ex)
		{
			return new OperationResult<string>
			{
				IsSuccess = false,
				CircuitState = _circuitBreaker.GetState(),
				ErrorMessage = ex.Message
			};
		}
	}

	public CircuitBreakerMetrics GetMetrics() => _circuitBreaker.GetMetrics();
}

/// <summary>
/// Regional cache service demonstrating regional isolation
/// </summary>
public class RegionalCacheService
{
	private readonly ICircuitBreakerFactory _circuitBreakerFactory;
	private readonly ConcurrentDictionary<string, double> _regionalFailureRates = new();
	private readonly ILogger<RegionalCacheService> _logger;

	public RegionalCacheService(ICircuitBreakerFactory circuitBreakerFactory, ILogger<RegionalCacheService> logger)
	{
		_circuitBreakerFactory = circuitBreakerFactory;
		_logger = logger;
	}

	public void SetRegionalFailureRate(string region, double failureRate)
	{
		_regionalFailureRates[region] = failureRate;
	}

	public async Task<OperationResult<string>> GetDataFromRegionAsync(string region, string key)
	{
		var circuitBreaker = _circuitBreakerFactory.GetOrCreate($"Region_{region}");
		var failureRate = _regionalFailureRates.GetValueOrDefault(region, 0.1);

		var value = await circuitBreaker.ExecuteAsync(
		async () =>
		{
			if (_random.NextDouble() < failureRate)
			{
				await Task.Delay(50);
				throw new InvalidOperationException($"Regional failure in {region}");
			}

			await Task.Delay(_random.Next(5, 20));
			return $"Data from {region} for {key}";
		},
		async () =>
		{
			await Task.Delay(2);
			return $"Fallback data from {region} for {key}";
		});

		return new OperationResult<string>
		{
			Value = value,
			IsSuccess = !value.StartsWith("Fallback"),
			IsFromFallback = value.StartsWith("Fallback"),
			CircuitState = circuitBreaker.GetState()
		};
	}

	public Dictionary<string, CircuitBreakerMetrics> GetAllRegionalMetrics()
	{
		return _circuitBreakerFactory.GetAllMetrics();
	}
}

/// <summary>
/// Adaptive TTL cache service that adjusts based on circuit breaker state
/// </summary>
public class AdaptiveTtlCacheService
{
	private readonly IDistributedCache _cache;
	private readonly ICircuitBreaker _circuitBreaker;
	private readonly ILogger _logger;
	private volatile bool _simulateFailures = false;

	public AdaptiveTtlCacheService(IDistributedCache cache, ICircuitBreakerFactory circuitBreakerFactory, ILogger logger)
	{
		_cache = cache;
		_circuitBreaker = circuitBreakerFactory.GetOrCreate("AdaptiveTtl");
		_logger = logger;
	}

	public void SimulateFailures(bool enable) => _simulateFailures = enable;

	public async Task<AdaptiveCacheResult<T>> GetWithAdaptiveTtlAsync<T>(string key, Func<Task<T>> dataFactory)
	{
		var state = _circuitBreaker.GetState();
		var baseTtl = TimeSpan.FromMinutes(5);

		// Adjust TTL based on circuit breaker state
		var effectiveTtl = state switch
		{
			CircuitState.Closed => baseTtl, // Normal TTL
			CircuitState.HalfOpen => baseTtl.Multiply(0.5), // Shorter TTL during testing
			CircuitState.Open => baseTtl.Multiply(2), // Longer TTL to reduce load
			_ => baseTtl
		};

		// Try to get from cache first
		var cachedData = await _cache.GetStringAsync(key);
		if (!string.IsNullOrEmpty(cachedData))
		{
			return new AdaptiveCacheResult<T>
			{
				Value = System.Text.Json.JsonSerializer.Deserialize<T>(cachedData)!,
				EffectiveTtl = effectiveTtl,
				IsFromCache = true
			};
		}

		// Get from source with circuit breaker protection
		var value = await _circuitBreaker.ExecuteAsync(
		async () =>
		{
			if (_simulateFailures && _random.NextDouble() < 0.6)
			{
				throw new InvalidOperationException("Simulated source failure");
			}
			return await dataFactory();
		},
		async () =>
		{
			// Fallback: return cached version with warning or default
			_logger.LogWarning("Using fallback for key {Key}", key);
			return await dataFactory(); // In real scenario, this might return a default or stale cached value
		});

		// Cache the result
		var serialized = System.Text.Json.JsonSerializer.Serialize(value);
		await _cache.SetStringAsync(key, serialized, new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = effectiveTtl
		});

		return new AdaptiveCacheResult<T>
		{
			Value = value,
			EffectiveTtl = effectiveTtl,
			IsFromCache = false
		};
	}
}

#endregion
}