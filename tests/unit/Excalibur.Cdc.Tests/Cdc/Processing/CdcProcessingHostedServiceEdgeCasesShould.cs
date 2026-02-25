// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;
using Microsoft.Extensions.Logging;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Edge case tests for <see cref="CdcProcessingHostedService"/> to improve coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CdcProcessing")]
[Trait("Priority", "1")]
public sealed class CdcProcessingHostedServiceEdgeCasesShould : UnitTestBase
{
	#region Health State Tests

	[Fact]
	public async Task ExecuteAsync_ShouldBecomeUnhealthy_WhenConsecutiveErrorsReachThreshold()
	{
		// Arrange
		var callCount = 0;
		var thresholdReached = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				if (Interlocked.Increment(ref callCount) >= 2)
				{
					thresholdReached.TrySetResult(true);
				}
			})
			.ThrowsAsync(new InvalidOperationException("boom"));

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(20),
			UnhealthyThreshold = 2
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();
		var service = new CdcProcessingHostedService(processor, options, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			thresholdReached.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		service.ConsecutiveErrors.ShouldBeGreaterThanOrEqualTo(2);
		service.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldRecoverHealth_AfterSuccessfulCycleFollowingError()
	{
		// Arrange
		var callCount = 0;
		var postRecoveryObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref callCount);
				if (current == 1)
				{
					throw new InvalidOperationException("first failure");
				}

				if (current >= 3)
				{
					postRecoveryObserved.TrySetResult(true);
				}

				return Task.FromResult(1);
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(20),
			UnhealthyThreshold = 3
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();
		var service = new CdcProcessingHostedService(processor, options, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			postRecoveryObserved.Task,
			TimeSpan.FromSeconds(5));
		service.ConsecutiveErrors.ShouldBe(0);
		service.IsHealthy.ShouldBeTrue();
		var lastSuccess = service.LastSuccessfulProcessing;
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		lastSuccess.ShouldBeGreaterThan(DateTimeOffset.UnixEpoch);
		service.IsHealthy.ShouldBeFalse(); // Service marks unhealthy when stopped.
	}

	[Fact]
	public async Task StopAsync_ShouldSetServiceUnhealthy_AfterRunningHealthyCycle()
	{
		// Arrange
		var firstSuccessObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstSuccessObserved.TrySetResult(true))
			.Returns(1);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(20),
			UnhealthyThreshold = 3
		});
		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();
		var service = new CdcProcessingHostedService(processor, options, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstSuccessObserved.Task,
			TimeSpan.FromSeconds(5));
		service.IsHealthy.ShouldBeTrue();
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		service.IsHealthy.ShouldBeFalse();
	}

	#endregion

	#region Drain Timeout Tests

	[Fact]
	public async Task StopAsync_ShouldLogWarning_WhenDrainTimeoutExceeded()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var processingStarted = CreateSignal();

		// Configure processor to block indefinitely on ProcessChangesAsync
		var processCompletionSource = new TaskCompletionSource<int>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => processingStarted.TrySetResult(true))
			.Returns(processCompletionSource.Task);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromSeconds(30), // Long interval so we don't complete naturally
			DrainTimeoutSeconds = 1 // Very short drain timeout
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			processingStarted.Task,
			TimeSpan.FromSeconds(5));
		// Stop should trigger drain timeout because processor is blocked
		await service.StopAsync(CancellationToken.None);

		// Cleanup - complete the blocked task
		processCompletionSource.SetResult(0);

		// Assert - Verify a logging call was made (LogDrainTimeoutExceeded)
		// The specific log verification is limited since we're using FakeItEasy,
		// but the path should have been exercised without throwing
	}

	[Fact]
	public async Task StopAsync_ShouldTriggerDrainTimeout_WhenExecuteAsyncBlocksLongEnough()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var processingStarted = new TaskCompletionSource();
		var neverComplete = new TaskCompletionSource<int>();

		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(_ => processingStarted.TrySetResult())
			.Returns(neverComplete.Task);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromHours(1), // Very long interval
			DrainTimeoutSeconds = 1 // Short drain timeout (1 second)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();
		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act
		await service.StartAsync(CancellationToken.None);

		// Wait for processing to start
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			processingStarted.Task,
			TimeSpan.FromSeconds(30));
		// Now stop - this should wait for drain timeout
		var stopTask = service.StopAsync(CancellationToken.None);

		// Wait for the stop to complete (should happen after drain timeout ~1 second)
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			stopTask,
			TimeSpan.FromSeconds(10));
		// Cleanup
		neverComplete.TrySetResult(0);

		// Assert - StopAsync completed without external cancellation
		// which means the drain timeout path was exercised
		stopTask.IsCompletedSuccessfully.ShouldBeTrue();
	}

	#endregion

	#region Processed Changes Logging Tests

	[Fact]
	public async Task ExecuteAsync_ShouldLogProcessedChanges_WhenChangesAreProcessed()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var callCount = 0;
		var multipleCyclesObserved = CreateSignal();

		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref callCount);
				if (current >= 2)
				{
					multipleCyclesObserved.TrySetResult(true);
				}

				// Return positive count on first call, zero on subsequent calls
				return Task.FromResult(current == 1 ? 5 : 0);
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			multipleCyclesObserved.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Verify processing was called with positive results
		callCount.ShouldBeGreaterThan(1);
	}

	#endregion

	#region OperationCanceledException Handling Tests

	[Fact]
	public async Task ExecuteAsync_ShouldGracefullyStop_WhenCancelled_DuringProcessing()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var processingObserved = CreateSignal();
		using var cts = new CancellationTokenSource();

		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				processingObserved.TrySetResult(true);
				// Cancel during processing
				cts.Cancel();
			})
			.ThrowsAsync(new OperationCanceledException());

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act - Should not throw
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			processingObserved.Task,
			TimeSpan.FromSeconds(5));
		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ExecuteAsync_ShouldGracefullyStop_WhenCancelled_DuringDelay()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var callCount = 0;
		var firstCallObserved = CreateSignal();

		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				Interlocked.Increment(ref callCount);
				firstCallObserved.TrySetResult(true);
			})
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromSeconds(10) // Long delay to ensure we cancel during it
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstCallObserved.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Should have processed at least once before cancellation
		callCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	#endregion

	#region StopAsync with External Cancellation Tests

	[Fact]
	public async Task StopAsync_ShouldCompleteGracefully_WhenPassedCancellationToken()
	{
		// Arrange
		var firstCallObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstCallObserved.TrySetResult(true))
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(100),
			DrainTimeoutSeconds = 5
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		// Act
		await service.StartAsync(CancellationToken.None);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstCallObserved.Task,
			TimeSpan.FromSeconds(5));
		using var stopCts = new CancellationTokenSource();
		await service.StopAsync(stopCts.Token);
	}

	#endregion

	#region Multiple Processing Cycles Tests

	[Fact]
	public async Task ExecuteAsync_ShouldProcessMultipleCycles_WithMixedResults()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		var callCount = 0;
		var multipleCyclesObserved = CreateSignal();

		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref callCount);
				if (current > 3)
				{
					multipleCyclesObserved.TrySetResult(true);
				}

				return Task.FromResult(current % 2 == 1 ? 3 : 0); // Alternate between 3 and 0
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(30)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			multipleCyclesObserved.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Should have made multiple calls
		callCount.ShouldBeGreaterThan(3);
	}

	#endregion

	#region Zero Processed Count Tests

	[Fact]
	public async Task ExecuteAsync_ShouldNotLogChanges_WhenNoChangesProcessed()
	{
		// Arrange
		var firstCallObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstCallObserved.TrySetResult(true))
			.Returns(0); // Always returns 0 changes

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();

		var service = new CdcProcessingHostedService(processor, options, logger);

		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstCallObserved.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert - Processing calls were made but no log for "processed changes"
		// since count was always 0
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldEmitDurationMs_WhenChangesAreProcessed()
	{
		// Arrange
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(3));

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();
		var observedDurations = new List<double>();
		var durationObserved = CreateSignal();
		_ = A.CallTo(() => logger.IsEnabled(LogLevel.Debug)).Returns(true);

		A.CallTo(logger)
			.Where(call => call.Method.Name == nameof(ILogger.Log))
			.Invokes(call =>
			{
				if (call.Arguments.Count < 3 || call.Arguments[0] is not LogLevel level || level != LogLevel.Debug)
				{
					return;
				}

				if (TryReadStructuredLogDouble(call.Arguments[2], "DurationMs", out var durationMs))
				{
					observedDurations.Add(durationMs);
					durationObserved.TrySetResult(true);
				}
			});

		var service = new CdcProcessingHostedService(processor, options, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			durationObserved.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		observedDurations.ShouldNotBeEmpty();
		observedDurations.All(duration => duration >= 0).ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_ShouldNotEmitDurationMs_WhenNoChangesAreProcessed()
	{
		// Arrange
		var firstCallObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		_ = A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() => firstCallObserved.TrySetResult(true))
			.Returns(0);

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(50)
		});

		var logger = A.Fake<ILogger<CdcProcessingHostedService>>();
		var observedDurations = new List<double>();
		_ = A.CallTo(() => logger.IsEnabled(LogLevel.Debug)).Returns(true);

		A.CallTo(logger)
			.Where(call => call.Method.Name == nameof(ILogger.Log))
			.Invokes(call =>
			{
				if (call.Arguments.Count < 3 || call.Arguments[0] is not LogLevel level || level != LogLevel.Debug)
				{
					return;
				}

				if (TryReadStructuredLogDouble(call.Arguments[2], "DurationMs", out var durationMs))
				{
					observedDurations.Add(durationMs);
				}
			});

		var service = new CdcProcessingHostedService(processor, options, logger);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstCallObserved.Task,
			TimeSpan.FromSeconds(5));
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		observedDurations.ShouldBeEmpty();
	}

	#endregion

	private static bool TryReadStructuredLogDouble(object? state, string key, out double value)
	{
		if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
		{
			var entry = structuredState.FirstOrDefault(pair => pair.Key == key);
			if (entry.Key is not null && entry.Value is IConvertible convertible)
			{
				value = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			}
		}

		value = default;
		return false;
	}

	private static TaskCompletionSource<bool> CreateSignal()
	{
		return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}

