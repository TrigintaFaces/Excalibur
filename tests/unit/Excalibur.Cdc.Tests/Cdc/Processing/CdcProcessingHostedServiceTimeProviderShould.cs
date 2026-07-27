// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Processing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Deterministic characterization tests for the <see cref="TimeProvider"/> seam in
/// <see cref="CdcProcessingHostedService"/>. These exercise the injected clock directly, so the
/// last-success stamp and the adaptive-poll / error-backoff delays are verified without any wall-clock
/// sleeping (the "no more real sleep" win).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CdcProcessing")]
public sealed class CdcProcessingHostedServiceTimeProviderShould : UnitTestBase
{
	/// <summary>
	/// The last-success timestamp is stamped from the injected <see cref="TimeProvider"/>, not the wall
	/// clock. With a fake clock pinned to a fixed instant, the recorded value equals that instant exactly —
	/// which the real clock (today's date) could never satisfy, so this binds the injection.
	/// </summary>
	[Fact]
	public async Task StampLastSuccessfulProcessing_FromTheInjectedClock()
	{
		// Arrange — a clock pinned far from "now" so equality can only hold via the injected provider.
		var pinnedInstant = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
		var fakeClock = new FakeTimeProvider(pinnedInstant);

		var successObserved = CreateSignal();
		var callCount = 0;
		var processor = A.Fake<ICdcBackgroundProcessor>();
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				// First cycle succeeds (stamps last-success), later cycles return 0 so the loop parks on a
				// fake-clock delay we never advance — no busy spin, no real sleep.
				var current = Interlocked.Increment(ref callCount);
				if (current == 1)
				{
					successObserved.TrySetResult(true);
					return Task.FromResult(1);
				}

				return Task.FromResult(0);
			});

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMinutes(5), // Large: the parked delay never elapses on the fake clock.
			UnhealthyThreshold = 10
		});

		var service = new CdcProcessingHostedService(
			SpWith(processor), options, fakeClock, NullLogger<CdcProcessingHostedService>.Instance);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			successObserved.Task, SignalWaitTimeout);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert — the stamp equals the fake clock's fixed instant (ticks-precision, UTC).
		service.LastSuccessfulProcessing.ShouldBe(new DateTimeOffset(pinnedInstant.UtcTicks, TimeSpan.Zero));
	}

	/// <summary>
	/// The empty-poll error backoff grows linearly with consecutive errors and caps at 5× the polling
	/// interval. Driving the loop through a delay-recording provider that fires timers immediately captures
	/// the exact requested delay sequence deterministically and without real sleeping.
	/// </summary>
	[Fact]
	public async Task ScaleErrorBackoffDelay_LinearlyThenCapAtFiveTimesTheInterval()
	{
		// Arrange
		const int pollingIntervalMs = 100;
		var recordingClock = new DelayRecordingTimeProvider();

		var enoughDelaysObserved = CreateSignal();
		var callCount = 0;
		var processor = A.Fake<ICdcBackgroundProcessor>();
		A.CallTo(() => processor.ProcessChangesAsync(A<CancellationToken>._))
			.Invokes(() =>
			{
				// Signal once we are guaranteed to have recorded the first six backoff delays.
				if (Interlocked.Increment(ref callCount) >= 7)
				{
					enoughDelaysObserved.TrySetResult(true);
				}
			})
			.ThrowsAsync(new InvalidOperationException("persistent error"));

		var options = Options.Create(new CdcProcessingOptions
		{
			Enabled = true,
			PollingInterval = TimeSpan.FromMilliseconds(pollingIntervalMs),
			UnhealthyThreshold = 100 // High so health state never interferes with the loop.
		});

		var service = new CdcProcessingHostedService(
			SpWith(processor), options, recordingClock, NullLogger<CdcProcessingHostedService>.Instance);
		using var cts = new CancellationTokenSource();

		// Act
		await service.StartAsync(cts.Token);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			enoughDelaysObserved.Task, SignalWaitTimeout);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert — delay = PollingInterval × min(consecutiveErrors, 5): grows 1→5 then caps.
		var delays = recordingClock.RecordedDelays();
		delays.Count.ShouldBeGreaterThanOrEqualTo(6);
		delays[0].ShouldBe(TimeSpan.FromMilliseconds(pollingIntervalMs * 1));
		delays[1].ShouldBe(TimeSpan.FromMilliseconds(pollingIntervalMs * 2));
		delays[2].ShouldBe(TimeSpan.FromMilliseconds(pollingIntervalMs * 3));
		delays[3].ShouldBe(TimeSpan.FromMilliseconds(pollingIntervalMs * 4));
		delays[4].ShouldBe(TimeSpan.FromMilliseconds(pollingIntervalMs * 5));
		delays[5].ShouldBe(TimeSpan.FromMilliseconds(pollingIntervalMs * 5)); // Capped, not 6×.
	}

	private static TaskCompletionSource<bool> CreateSignal()
		=> new(TaskCreationOptions.RunContinuationsAsynchronously);

	private static IServiceProvider SpWith(ICdcBackgroundProcessor processor)
		=> new ServiceCollection().AddSingleton(processor).BuildServiceProvider();

	private static TimeSpan SignalWaitTimeout
		=> global::Tests.Shared.Infrastructure.TestTimeouts.Integration;

	/// <summary>
	/// A minimal <see cref="TimeProvider"/> that records the requested delay of every timer and fires it
	/// immediately, so a delay-driven loop iterates instantly while the exact delay sequence is captured.
	/// </summary>
	private sealed class DelayRecordingTimeProvider : TimeProvider
	{
		private readonly List<TimeSpan> _delays = [];
		private readonly object _gate = new();

		public IReadOnlyList<TimeSpan> RecordedDelays()
		{
			lock (_gate)
			{
				return _delays.ToArray();
			}
		}

		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			lock (_gate)
			{
				_delays.Add(dueTime);
			}

			return new ImmediateTimer(callback, state);
		}

		private sealed class ImmediateTimer : ITimer
		{
			public ImmediateTimer(TimerCallback callback, object? state)
				=> _ = ThreadPool.QueueUserWorkItem(_ => callback(state));

			public bool Change(TimeSpan dueTime, TimeSpan period) => true;

			public void Dispose()
			{
				// No unmanaged state to release; the callback has already been scheduled.
			}

			public ValueTask DisposeAsync() => ValueTask.CompletedTask;
		}
	}
}
