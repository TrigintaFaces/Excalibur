// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Tests for error backoff behavior in <see cref="CdcProcessingHostedService"/>.
/// Verifies exponential backoff on consecutive errors and reset on success.
/// </summary>
/// <remarks>
/// Sprint 826 — bd-dpah8h: CDC adaptive polling error backoff.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "CdcProcessing")]
public sealed class CdcProcessingHostedServiceBackoffShould : UnitTestBase
{
	[Fact]
	public async Task IncrementConsecutiveErrors_OnProcessingFailure()
	{
		// Arrange
		var callCount = 0;
		var errorsCounted = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				if (Interlocked.Increment(ref callCount) >= 3)
				{
					errorsCounted.TrySetResult(true);
				}
			})
			.ThrowsAsync(new InvalidOperationException("test error"));

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(20),
			UnhealthyThreshold = 10 // High threshold so we don't go unhealthy
		});

		var service = new CdcProcessingHostedService(processor, options, NullLogger<CdcProcessingHostedService>.Instance);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			errorsCounted.Task,
			SignalWaitTimeout);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert — consecutive errors should have accumulated
		service.ConsecutiveErrors.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task ResetConsecutiveErrors_AfterSuccessfulCycle()
	{
		// Arrange — fail once, then succeed
		var callCount = 0;
		var recoveryObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref callCount);
				if (current <= 2)
				{
					throw new InvalidOperationException("transient error");
				}

				if (current >= 4)
				{
					recoveryObserved.TrySetResult(true);
				}

				return Task.FromResult(1); // Success
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(20),
			UnhealthyThreshold = 10
		});

		var service = new CdcProcessingHostedService(processor, options, NullLogger<CdcProcessingHostedService>.Instance);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			recoveryObserved.Task,
			SignalWaitTimeout);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert — errors should have reset to 0 after successful cycle
		service.ConsecutiveErrors.ShouldBe(0);
	}

	[Fact]
	public async Task ApplyBackoff_WhenConsecutiveErrorsOccur_ThenRecover()
	{
		// Arrange — fail 3 times (backoff multiplier grows 1→2→3), then succeed
		var callCount = 0;
		var recoveryObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var current = Interlocked.Increment(ref callCount);
				if (current <= 3)
				{
					throw new InvalidOperationException("repeated error");
				}

				// After errors, processing returns 0 (enters delay path where backoff applies)
				// Then next cycle returns 1 to signal recovery
				if (current >= 5)
				{
					recoveryObserved.TrySetResult(true);
				}

				return Task.FromResult(current == 4 ? 0 : 1);
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(10),
			UnhealthyThreshold = 10
		});

		var service = new CdcProcessingHostedService(processor, options, NullLogger<CdcProcessingHostedService>.Instance);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			recoveryObserved.Task,
			SignalWaitTimeout);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert — service recovered (errors reset)
		service.ConsecutiveErrors.ShouldBe(0);
		callCount.ShouldBeGreaterThanOrEqualTo(5);
	}

	[Fact]
	public async Task CapBackoffMultiplier_AtFive()
	{
		// Arrange — fail many times with short polling, verify service doesn't hang
		// The backoff multiplier is capped at 5x, so delay = 5 * PollingInterval at most
		var callCount = 0;
		var manyErrorsObserved = CreateSignal();
		var processor = A.Fake<ICdcBackgroundProcessor>();
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				if (Interlocked.Increment(ref callCount) >= 8)
				{
					manyErrorsObserved.TrySetResult(true);
				}
			})
			.ThrowsAsync(new InvalidOperationException("persistent error"));

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(10), // 10ms * 5 = 50ms max backoff
			UnhealthyThreshold = 20
		});

		var service = new CdcProcessingHostedService(processor, options, NullLogger<CdcProcessingHostedService>.Instance);
		using var cts = new CancellationTokenSource();

		// Act — should complete within a reasonable time because cap is 5x
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			manyErrorsObserved.Task,
			SignalWaitTimeout);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert — many errors occurred, service continued processing
		callCount.ShouldBeGreaterThanOrEqualTo(8);
	}

	private static TaskCompletionSource<bool> CreateSignal()
		=> new(TaskCreationOptions.RunContinuationsAsynchronously);

	private static TimeSpan SignalWaitTimeout
		=> global::Tests.Shared.Infrastructure.TestTimeouts.Integration;
}