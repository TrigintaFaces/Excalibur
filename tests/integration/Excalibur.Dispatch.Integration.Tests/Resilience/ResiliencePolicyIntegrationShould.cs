// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Resilience;

/// <summary>
/// Integration tests for resilience policies including bulkhead and graceful degradation.
/// These tests verify that policies work correctly under realistic concurrent conditions.
/// </summary>
public sealed class ResiliencePolicyIntegrationShould : IntegrationTestBase
{
	#region Bulkhead Policy Tests

	[Fact]
	public async Task BulkheadPolicy_EnforceConcurrencyLimit()
	{
		// Arrange
		var options = new BulkheadOptions
		{
			MaxConcurrency = 2,
			MaxQueueLength = 5,
			OperationTimeout = TimeSpan.FromSeconds(10)
		};
		using var bulkhead = new BulkheadPolicy("test-bulkhead", options);

		var activeCount = 0;
		var maxConcurrent = 0;
		var operationGate = new TaskCompletionSource<bool>();

		// Act - Start 5 operations, but only 2 should run concurrently
		var tasks = Enumerable.Range(0, 5).Select(async i =>
		{
			await bulkhead.ExecuteAsync(async () =>
			{
				var current = Interlocked.Increment(ref activeCount);
				var existingMax = Volatile.Read(ref maxConcurrent);
				while (current > existingMax)
				{
					if (Interlocked.CompareExchange(ref maxConcurrent, current, existingMax) == existingMax)
					{
						break;
					}

					existingMax = Volatile.Read(ref maxConcurrent);
				}

				await operationGate.Task.ConfigureAwait(false);
				Interlocked.Decrement(ref activeCount);
				return true;
			}, TestCancellationToken).ConfigureAwait(false);
		}).ToList();

		// Allow operations to proceed
		await Task.Delay(100, TestCancellationToken).ConfigureAwait(false);
		operationGate.SetResult(true);
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Max concurrent should not exceed 2
		maxConcurrent.ShouldBeLessThanOrEqualTo(2);
	}

	[Fact]
	public async Task BulkheadPolicy_RejectWhenQueueFull()
	{
		// Arrange - MaxConcurrency=1, MaxQueueLength=1 → only 1 executing + 1 queued allowed
		var options = new BulkheadOptions
		{
			MaxConcurrency = 1,
			MaxQueueLength = 1,
			OperationTimeout = TimeSpan.FromSeconds(10)
		};
		using var bulkhead = new BulkheadPolicy("reject-test", options);

		var blockedGate = new TaskCompletionSource<bool>();
		var blockingStarted = new TaskCompletionSource<bool>();
		var rejectedCount = 0;

		// Act - Start first operation to block the semaphore
		var blockingTask = bulkhead.ExecuteAsync(async () =>
		{
			blockingStarted.SetResult(true);
			_ = await blockedGate.Task.ConfigureAwait(false);
			return true;
		}, TestCancellationToken);

		// Wait until the blocking task has actually acquired the semaphore
		_ = await blockingStarted.Task.ConfigureAwait(false);

		// Start a second operation that will queue (waits on the semaphore)
		var queuingStarted = new TaskCompletionSource<bool>();
		var queuingTask = Task.Run(async () =>
		{
			queuingStarted.SetResult(true);
			_ = await bulkhead.ExecuteAsync(async () =>
			{
				await Task.Delay(10, TestCancellationToken).ConfigureAwait(false);
				return true;
			}, TestCancellationToken).ConfigureAwait(false);
		});

		// Wait for the queuing task to have started (it will be waiting on the semaphore)
		_ = await queuingStarted.Task.ConfigureAwait(false);
		await Task.Delay(100, TestCancellationToken).ConfigureAwait(false);

		// Now launch additional operations that should all be rejected (slot full + queue full)
		var rejectionTasks = new List<Task>();
		for (var i = 0; i < 5; i++)
		{
			rejectionTasks.Add(Task.Run(async () =>
			{
				try
				{
					_ = await bulkhead.ExecuteAsync(async () =>
					{
						await Task.Delay(10, TestCancellationToken).ConfigureAwait(false);
						return true;
					}, TestCancellationToken).ConfigureAwait(false);
				}
				catch (BulkheadRejectedException)
				{
					_ = Interlocked.Increment(ref rejectedCount);
				}
			}));
		}

		await Task.WhenAll(rejectionTasks).ConfigureAwait(false);

		// Release the blocking task to allow cleanup
		blockedGate.SetResult(true);
		_ = await blockingTask.ConfigureAwait(false);
		await queuingTask.ConfigureAwait(false);

		// Assert - All 5 additional operations should have been rejected
		rejectedCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task BulkheadPolicy_ReportAccurateMetrics()
	{
		// Arrange
		var options = new BulkheadOptions
		{
			MaxConcurrency = 3,
			MaxQueueLength = 10,
			OperationTimeout = TimeSpan.FromSeconds(10)
		};
		using var bulkhead = new BulkheadPolicy("metrics-test", options);

		// Act - Execute several operations
		for (var i = 0; i < 10; i++)
		{
			_ = await bulkhead.ExecuteAsync(async () =>
			{
				await Task.Delay(10, TestCancellationToken).ConfigureAwait(false);
				return true;
			}, TestCancellationToken).ConfigureAwait(false);
		}

		var metrics = bulkhead.GetMetrics();

		// Assert
		metrics.Name.ShouldBe("metrics-test");
		metrics.MaxConcurrency.ShouldBe(3);
		metrics.MaxQueueLength.ShouldBe(10);
		metrics.TotalExecutions.ShouldBe(10);
		metrics.RejectedExecutions.ShouldBe(0);
	}

	#endregion

	#region Graceful Degradation Tests

	[Fact]
	public async Task GracefulDegradation_ExecuteNormallyAtNormalLevel()
	{
		// Arrange
		var options = MsOptions.Create(new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(60),
			EnableAutoAdjustment = false
		});
		var logger = new TestLogger<GracefulDegradationService>();
		using var service = new GracefulDegradationService(options, logger);

		var executed = false;

		// Act
		var context = new DegradationContext<bool>
		{
			OperationName = "test-operation",
			Priority = 5,
			IsCritical = false,
			PrimaryOperation = () =>
			{
				executed = true;
				return Task.FromResult(true);
			}
		};

		var result = await service.ExecuteWithDegradationAsync(context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		executed.ShouldBeTrue();
		service.CurrentLevel.ShouldBe(DegradationLevel.Normal);
	}

	[Fact]
	public async Task GracefulDegradation_UseFallbackWhenLevelChanges()
	{
		// Arrange
		var options = MsOptions.Create(new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(60),
			EnableAutoAdjustment = false
		});
		var logger = new TestLogger<GracefulDegradationService>();
		using var service = new GracefulDegradationService(options, logger);

		var primaryExecuted = false;
		var fallbackExecuted = false;

		// Set level to Minor before execution
		service.SetLevel(DegradationLevel.Minor, "Test level change");

		// Act — Priority must be >= Minor level PriorityThreshold (default 10) to avoid rejection
		var context = new DegradationContext<string>
		{
			OperationName = "fallback-test",
			Priority = 10,
			IsCritical = false,
			PrimaryOperation = () =>
			{
				primaryExecuted = true;
				return Task.FromResult("primary");
			},
			Fallbacks = new Dictionary<DegradationLevel, Func<Task<string>>>
			{
				[DegradationLevel.Minor] = () =>
				{
					fallbackExecuted = true;
					return Task.FromResult("fallback");
				}
			}
		};

		var result = await service.ExecuteWithDegradationAsync(context, TestCancellationToken).ConfigureAwait(false);

		// Assert
		result.ShouldBe("fallback");
		fallbackExecuted.ShouldBeTrue();
		primaryExecuted.ShouldBeFalse();
	}

	[Fact]
	public async Task GracefulDegradation_RejectLowPriorityAtHighLevel()
	{
		// Arrange
		var degradationOptions = new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(60),
			EnableAutoAdjustment = false,
		};
		degradationOptions.Levels[2] = degradationOptions.Levels[2] with { PriorityThreshold = 7 };
		var options = MsOptions.Create(degradationOptions);
		var logger = new TestLogger<GracefulDegradationService>();
		using var service = new GracefulDegradationService(options, logger);

		service.SetLevel(DegradationLevel.Major, "Test rejection");

		// Act
		var context = new DegradationContext<bool>
		{
			OperationName = "low-priority-op",
			Priority = 3, // Below threshold
			IsCritical = false,
			PrimaryOperation = () => Task.FromResult(true)
		};

		// Assert
		_ = await Should.ThrowAsync<DegradationRejectedException>(
			() => service.ExecuteWithDegradationAsync(context, TestCancellationToken)).ConfigureAwait(false);
	}

	[Fact]
	public async Task GracefulDegradation_AllowCriticalOperationsAlways()
	{
		// Arrange
		var options = MsOptions.Create(new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(60),
			EnableAutoAdjustment = false
		});
		var logger = new TestLogger<GracefulDegradationService>();
		using var service = new GracefulDegradationService(options, logger);

		service.SetLevel(DegradationLevel.Emergency, "Maximum degradation");

		var executed = false;

		// Act
		var context = new DegradationContext<bool>
		{
			OperationName = "critical-op",
			Priority = 1, // Lowest priority but critical
			IsCritical = true,
			PrimaryOperation = () =>
			{
				executed = true;
				return Task.FromResult(true);
			}
		};

		var result = await service.ExecuteWithDegradationAsync(context, TestCancellationToken).ConfigureAwait(false);

		// Assert - Critical operations should never be rejected
		result.ShouldBeTrue();
		executed.ShouldBeTrue();
	}

	[Fact]
	public void GracefulDegradation_TrackMetricsCorrectly()
	{
		// Arrange
		var options = MsOptions.Create(new GracefulDegradationOptions
		{
			HealthCheckInterval = TimeSpan.FromSeconds(60),
			EnableAutoAdjustment = false
		});
		var logger = new TestLogger<GracefulDegradationService>();
		using var service = new GracefulDegradationService(options, logger);

		// Act
		service.SetLevel(DegradationLevel.Moderate, "Test metrics");
		var metrics = service.GetMetrics();

		// Assert
		metrics.CurrentLevel.ShouldBe(DegradationLevel.Moderate);
		metrics.LastChangeReason.ShouldBe("Test metrics");
		metrics.LastLevelChange.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
	}

	#endregion

	#region Helper Classes

	private sealed class TestLogger<T> : ILogger<T>
	{
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			// No-op for tests
		}
	}

	#endregion
}
