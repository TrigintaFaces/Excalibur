using System;
using System.Threading.Tasks;
using Excalibur.Dispatch.Resilience.Polly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples;

/// <summary>
///     Example demonstrating the comprehensive resilience improvements.
/// </summary>
public class ResilienceExample
{
	public static async Task Main(string[] args)
	{
		var host = Host.CreateDefaultBuilder(args)
			.ConfigureServices((context, services) =>
			{
				// Configure logging
				services.AddLogging(builder =>
				{
					builder.AddConsole();
					builder.SetMinimumLevel(LogLevel.Information);
				});

				// Add resilience services with configuration
				services.AddPollyResilience(context.Configuration);

				// Configure timeout management
				services.ConfigureTimeoutManager(options =>
				{
					options.DefaultTimeout = TimeSpan.FromSeconds(30);
					options.DatabaseTimeout = TimeSpan.FromSeconds(15);
					options.HttpTimeout = TimeSpan.FromSeconds(100);
					options.CacheTimeout = TimeSpan.FromSeconds(5);
					options.LogTimeoutWarnings = true;
					options.SlowOperationThreshold = 0.8;
				});

				// Configure enhanced retry with jitter
				services.AddEnhancedRetryPolicy("api-retry", options =>
				{
					options.MaxRetries = 5;
					options.BaseDelay = TimeSpan.FromSeconds(1);
					options.JitterStrategy = JitterStrategy.Equal; // Prevents thundering herd
					options.EnhancedBackoffStrategy = EnhancedBackoffStrategy.Exponential;
					options.MaxDelay = TimeSpan.FromSeconds(30);
					options.UseJitter = true;
					options.ShouldRetry = ex => ex is not ArgumentException; // Don't retry bad arguments
				});

				// Configure bulkhead isolation
				services.AddBulkhead("database-operations", options =>
				{
					options.MaxConcurrency = 10;
					options.MaxQueueLength = 50;
					options.OperationTimeout = TimeSpan.FromSeconds(15);
					options.AllowQueueing = true;
				});

				services.AddBulkhead("external-api", options =>
				{
					options.MaxConcurrency = 5;
					options.MaxQueueLength = 20;
					options.OperationTimeout = TimeSpan.FromSeconds(30);
				});

				// Configure distributed circuit breaker
				services.AddDistributedCircuitBreaker("payment-service", options =>
				{
					options.FailureRatio = 0.5;
					options.MinimumThroughput = 10;
					options.BreakDuration = TimeSpan.FromSeconds(30);
					options.ConsecutiveFailureThreshold = 5;
					options.SuccessThresholdToClose = 3;
					options.SyncInterval = TimeSpan.FromSeconds(5);
				});

				// Configure graceful degradation using Levels collection
				services.ConfigureGracefulDegradation(options =>
				{
					options.Levels =
					[
						new("Minor", PriorityThreshold: 10, ErrorRateThreshold: 0.01, CpuThreshold: 60, MemoryThreshold: 60),
						new("Moderate", PriorityThreshold: 30, ErrorRateThreshold: 0.05, CpuThreshold: 70, MemoryThreshold: 70),
						new("Major", PriorityThreshold: 50, ErrorRateThreshold: 0.10, CpuThreshold: 80, MemoryThreshold: 80),
						new("Severe", PriorityThreshold: 70, ErrorRateThreshold: 0.25, CpuThreshold: 90, MemoryThreshold: 90),
						new("Emergency", PriorityThreshold: 100, ErrorRateThreshold: 0.50, CpuThreshold: 95, MemoryThreshold: 95),
					];

					// Auto-adjustment based on health metrics
					options.EnableAutoAdjustment = true;
					options.HealthCheckInterval = TimeSpan.FromSeconds(30);
				});

				// Add the example service
				services.AddHostedService<ResilienceExampleService>();
			})
			.Build();

		await host.RunAsync();
	}
}

/// <summary>
///     Example service demonstrating resilience patterns.
/// </summary>
public class ResilienceExampleService : BackgroundService
{
	private readonly ILogger<ResilienceExampleService> _logger;
	private readonly ITimeoutManager _timeoutManager;
	private readonly BulkheadManager _bulkheadManager;
	private readonly IGracefulDegradationService _degradationService;
	private readonly DistributedCircuitBreakerFactory _circuitBreakerFactory;
	private readonly IServiceProvider _serviceProvider;

	public ResilienceExampleService(
		ILogger<ResilienceExampleService> logger,
		ITimeoutManager timeoutManager,
		BulkheadManager bulkheadManager,
		IGracefulDegradationService degradationService,
		DistributedCircuitBreakerFactory circuitBreakerFactory,
		IServiceProvider serviceProvider)
	{
		_logger = logger;
		_timeoutManager = timeoutManager;
		_bulkheadManager = bulkheadManager;
		_degradationService = degradationService;
		_circuitBreakerFactory = circuitBreakerFactory;
		_serviceProvider = serviceProvider;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Resilience Example Service started");

		// Example 1: Enhanced Retry with Jitter
		await DemonstrateEnhancedRetryWithJitter();

		// Example 2: Centralized Timeout Management
		await DemonstrateCentralizedTimeouts();

		// Example 3: Bulkhead Isolation
		await DemonstrateBulkheadIsolation();

		// Example 4: Graceful Degradation
		await DemonstrateGracefulDegradation();

		// Example 5: Distributed Circuit Breaker
		await DemonstrateDistributedCircuitBreaker();

		_logger.LogInformation("Resilience examples completed");
	}

	private async Task DemonstrateEnhancedRetryWithJitter()
	{
		_logger.LogInformation("=== Enhanced Retry with Jitter Demo ===");

		var retryPolicy = new EnhancedRetryPolicy(
			new EnhancedRetryOptions
			{
				MaxRetries = 3,
				BaseDelay = TimeSpan.FromSeconds(1),
				JitterStrategy = JitterStrategy.Equal,
				EnhancedBackoffStrategy = EnhancedBackoffStrategy.Exponential,
				UseJitter = true
			},
			_logger,
			_timeoutManager);

		var attemptCount = 0;

		try
		{
			var result = await retryPolicy.ExecuteAsync(async () =>
			{
				attemptCount++;
				_logger.LogInformation($"Attempt {attemptCount} with jittered delay");

				if (attemptCount < 3)
				{
					throw new InvalidOperationException("Simulated transient failure");
				}

				return "Success after retries with jitter!";
			}, "demo-operation");

			_logger.LogInformation($"Operation succeeded: {result}");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Operation failed after all retries");
		}
	}

	private async Task DemonstrateCentralizedTimeouts()
	{
		_logger.LogInformation("=== Centralized Timeout Management Demo ===");

		// Register custom timeout
		_timeoutManager.RegisterTimeout("CustomOperation", TimeSpan.FromSeconds(10));

		// Get various timeouts
		var dbTimeout = _timeoutManager.GetTimeout("Database.Query");
		var httpTimeout = _timeoutManager.GetTimeout("Http.Get");
		var customTimeout = _timeoutManager.GetTimeout("CustomOperation");
		var defaultTimeout = _timeoutManager.DefaultTimeout;

		_logger.LogInformation($"Database timeout: {dbTimeout}");
		_logger.LogInformation($"HTTP timeout: {httpTimeout}");
		_logger.LogInformation($"Custom timeout: {customTimeout}");
		_logger.LogInformation($"Default timeout: {defaultTimeout}");

		await Task.CompletedTask;
	}

	private async Task DemonstrateBulkheadIsolation()
	{
		_logger.LogInformation("=== Bulkhead Isolation Demo ===");

		var bulkhead = _bulkheadManager.GetOrCreateBulkhead("demo-resource", new BulkheadOptions
		{
			MaxConcurrency = 3,
			MaxQueueLength = 2,
			OperationTimeout = TimeSpan.FromSeconds(5)
		});

		// Simulate multiple concurrent operations
		var tasks = Enumerable.Range(1, 8).Select(i => Task.Run(async () =>
		{
			try
			{
				await bulkhead.ExecuteAsync(async () =>
				{
					_logger.LogInformation($"Operation {i} executing in bulkhead");
					await Task.Delay(2000); // Simulate work
					return i;
				});
				_logger.LogInformation($"Operation {i} completed");
			}
			catch (BulkheadRejectedException)
			{
				_logger.LogWarning($"Operation {i} rejected - bulkhead at capacity");
			}
		}));

		await Task.WhenAll(tasks);

		var metrics = bulkhead.GetMetrics();
		_logger.LogInformation($"Bulkhead metrics - Total: {metrics.TotalExecutions}, Rejected: {metrics.RejectedExecutions}");
	}

	private async Task DemonstrateGracefulDegradation()
	{
		_logger.LogInformation("=== Graceful Degradation Demo ===");

		// Create a degradation context with multiple fallback levels
		var context = new DegradationContext<string>
		{
			OperationName = "DataProcessing",
			Priority = 50,
			IsCritical = false,
			PrimaryOperation = async () =>
			{
				_logger.LogInformation("Executing primary operation (full features)");
				await Task.Delay(100);
				// Simulate failure in normal mode
				if (_degradationService.CurrentLevel == DegradationLevel.Normal)
				{
					throw new InvalidOperationException("Primary operation failed");
				}
				return "Primary result";
			},
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
			{
				[DegradationLevel.Minor] = async () =>
				{
					_logger.LogInformation("Executing minor degradation (reduced features)");
					await Task.Delay(50);
					return "Minor degradation result - some features disabled";
				},
				[DegradationLevel.Moderate] = async () =>
				{
					_logger.LogInformation("Executing moderate degradation (essential features only)");
					await Task.Delay(20);
					return "Moderate degradation result - essential features only";
				},
				[DegradationLevel.Major] = async () =>
				{
					_logger.LogInformation("Executing major degradation (critical features only)");
					return "Major degradation result - minimal functionality";
				}
			}
		};

		// Test at different degradation levels
		foreach (var level in new[] { DegradationLevel.Normal, DegradationLevel.Minor, DegradationLevel.Moderate })
		{
			if (level != DegradationLevel.Normal)
			{
				_degradationService.SetLevel(level, $"Testing {level} degradation");
			}

			try
			{
				var result = await _degradationService.ExecuteWithDegradationAsync(context);
				_logger.LogInformation($"At {level} level: {result}");
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Operation failed at {level} level: {ex.Message}");
			}
		}

		// Reset to normal
		_degradationService.SetLevel(DegradationLevel.Normal, "Returning to normal");

		var metrics = _degradationService.GetMetrics();
		_logger.LogInformation($"Degradation metrics - Total ops: {metrics.TotalOperations}, Fallbacks: {metrics.TotalFallbacks}");
	}

	private async Task DemonstrateDistributedCircuitBreaker()
	{
		_logger.LogInformation("=== Distributed Circuit Breaker Demo ===");

		var circuitBreaker = _circuitBreakerFactory.GetOrCreate("demo-service");

		// Simulate failures to open the circuit
		for (int i = 1; i <= 6; i++)
		{
			try
			{
				await circuitBreaker.ExecuteAsync(async () =>
				{
					_logger.LogInformation($"Attempt {i} through circuit breaker");
					if (i <= 4)
					{
						throw new InvalidOperationException("Service unavailable");
					}
					return "Success";
				});
			}
			catch (Exception ex)
			{
				_logger.LogWarning($"Attempt {i} failed: {ex.Message}");
				await circuitBreaker.RecordFailureAsync(ex);
			}
		}

		var state = await circuitBreaker.GetStateAsync();
		_logger.LogInformation($"Circuit breaker state: {state}");

		// Wait for circuit to recover
		_logger.LogInformation("Waiting for circuit to recover...");
		await Task.Delay(2000);

		// Record successes to close circuit
		await circuitBreaker.RecordSuccessAsync();
		await circuitBreaker.RecordSuccessAsync();
		await circuitBreaker.RecordSuccessAsync();

		state = await circuitBreaker.GetStateAsync();
		_logger.LogInformation($"Circuit breaker state after recovery: {state}");
	}
}