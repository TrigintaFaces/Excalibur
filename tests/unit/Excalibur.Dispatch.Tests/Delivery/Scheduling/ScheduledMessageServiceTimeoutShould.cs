// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

/// <summary>
/// Tests for <see cref="ScheduledMessageService"/> timeout integration
/// (ITimePolicy / ITimeoutMonitor parameters).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ScheduledMessageServiceTimeoutShould : IDisposable
{
	private readonly ServiceProvider _scopeProvider = new ServiceCollection().BuildServiceProvider();
	public void Dispose() => _scopeProvider.Dispose();

	private ScheduledMessageService CreateService(
		IScheduleStore? store = null,
		ITimePolicy? timePolicy = null,
		ITimeoutMonitor? timeoutMonitor = null,
		ILogger<ScheduledMessageService>? logger = null,
		TimeProvider? timeProvider = null)
	{
		return new ScheduledMessageService(
			store ?? A.Fake<IScheduleStore>(),
			A.Fake<IDispatcher>(),
			new DispatchJsonSerializer(),
			A.Fake<ICronScheduler>(),
			Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
			{
				PollInterval = TimeSpan.FromMilliseconds(50),
			EnableAdaptivePolling = false
			}),
			Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()),
			logger ?? NullLogger<ScheduledMessageService>.Instance,
			_scopeProvider.GetRequiredService<IServiceScopeFactory>(),
			timePolicy,
			timeoutMonitor, timeProvider);
	}

	/// <summary>
	/// Creates a fake that implements both ITimePolicy and ITimePolicyConfiguration
	/// so ShouldApplyTimeout can be controlled. Optional completion sources are signalled
	/// when the corresponding member is invoked, enabling deterministic (signal-based) waits
	/// instead of wall-clock delays.
	/// </summary>
	private static ITimePolicy CreateConfigurableTimePolicy(
		bool shouldApply,
		TimeSpan timeout,
		TaskCompletionSource? shouldApplyCalled = null,
		TaskCompletionSource? getTimeoutCalled = null)
	{
		// FakeItEasy: create a fake that implements both interfaces
		var policy = A.Fake<ITimePolicy>(o => o.Implements<ITimePolicyConfiguration>());

		_ = A.CallTo(() => ((ITimePolicyConfiguration)policy).ShouldApplyTimeout(
				A<TimeoutOperationType>._, A<TimeoutContext?>._))
			.Invokes(() => shouldApplyCalled?.TrySetResult())
			.Returns(shouldApply);

		_ = A.CallTo(() => policy.GetTimeoutFor(A<TimeoutOperationType>._))
			.Invokes(() => getTimeoutCalled?.TrySetResult())
			.Returns(timeout);

		return policy;
	}

	[Fact]
	public void Constructor_AcceptsNullTimePolicy()
	{
		var service = CreateService(timePolicy: null);
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsNullTimeoutMonitor()
	{
		var service = CreateService(timeoutMonitor: null);
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptsBothTimePolicyAndMonitor()
	{
		var service = CreateService(
			timePolicy: A.Fake<ITimePolicy>(),
			timeoutMonitor: A.Fake<ITimeoutMonitor>());
		_ = service.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteAsync_WithNoTimePolicy_ProcessesNormally()
	{
		// Arrange -- signal the moment the store is actually polled (deterministic, no wall-clock guessing).
		var polled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Invokes(() => polled.TrySetResult())
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>()));

		var service = CreateService(store: store, timePolicy: null);

		// Act -- start with a non-expiring token so StopAsync alone drives shutdown.
		// (BackgroundService links the start token into its stopping token; a short-lived
		// token can cancel the loop before its first iteration under CI load.)
		await service.StartAsync(CancellationToken.None);
		try
		{
			// Wait on the signal OR on the background loop ending, whichever happens first.
			//
			// This exists because of a real CI failure that taught nothing. The test timed out after
			// 30 seconds against a 50ms poll interval -- roughly 600 missed poll cycles -- and all it
			// reported was "Timed out waiting for signal". A timeout that large is not slowness: the
			// loop did not run. But BackgroundService swallows an ExecuteAsync fault into its
			// ExecuteTask, so if the loop threw before its first poll, the only symptom is the
			// signal never arriving, and the actual exception is never surfaced by the assertion.
			//
			// Awaiting ExecuteTask when it finishes first re-throws whatever ExecuteAsync threw, so
			// the next occurrence names its own cause instead of blaming the clock. This does NOT
			// widen the timeout, and does not make the test pass when it would have failed -- it
			// changes an unactionable flake into a diagnosable one.
			var loop = service.ExecuteTask;
			if (loop is null)
			{
				throw new InvalidOperationException(
					"ExecuteTask was null after StartAsync: the background loop was never started.");
			}

			var finished = await Task.WhenAny(polled.Task, loop);
			if (!ReferenceEquals(finished, polled.Task))
			{
				// Re-throws the real fault if ExecuteAsync threw.
				await loop;

				throw new InvalidOperationException(
					"The scheduler loop ended without ever polling the store. ExecuteAsync completed "
					+ "without faulting and without calling GetAllAsync, so the first iteration was "
					+ "skipped rather than delayed.");
			}

			await WaitHelpers.AwaitSignalAsync(polled.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- store was polled normally
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WithActiveTimePolicy_AppliesTimeout()
	{
		// Arrange -- policy that says "yes, apply timeout" with generous duration.
		// Signal when GetTimeoutFor runs, which only happens on the active path.
		var getTimeoutCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timePolicy = CreateConfigurableTimePolicy(
			shouldApply: true,
			timeout: TimeSpan.FromSeconds(30),
			getTimeoutCalled: getTimeoutCalled);

		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>()));

		var service = CreateService(store: store, timePolicy: timePolicy);

		// Act
		await service.StartAsync(CancellationToken.None);
		try
		{
			await WaitHelpers.AwaitSignalAsync(getTimeoutCalled.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- time policy was consulted and timeout was applied
		A.CallTo(() => ((ITimePolicyConfiguration)timePolicy).ShouldApplyTimeout(
				A<TimeoutOperationType>._, A<TimeoutContext?>._))
			.MustHaveHappened();
		A.CallTo(() => timePolicy.GetTimeoutFor(A<TimeoutOperationType>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WithInactiveTimePolicy_SkipsGetTimeoutFor()
	{
		// Arrange -- policy that says "no, don't apply timeout".
		// Signal when ShouldApplyTimeout is consulted so we can wait for the real call
		// rather than a fixed delay.
		var shouldApplyCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timePolicy = CreateConfigurableTimePolicy(
			shouldApply: false,
			timeout: TimeSpan.FromSeconds(5),
			shouldApplyCalled: shouldApplyCalled);

		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>()));

		var service = CreateService(store: store, timePolicy: timePolicy);

		// Act
		await service.StartAsync(CancellationToken.None);
		try
		{
			await WaitHelpers.AwaitSignalAsync(shouldApplyCalled.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- ShouldApplyTimeout consulted, but GetTimeoutFor NOT called.
		// GetTimeoutFor is unreachable while ShouldApplyTimeout returns false, so this holds
		// regardless of how many poll iterations elapse before shutdown.
		A.CallTo(() => ((ITimePolicyConfiguration)timePolicy).ShouldApplyTimeout(
				A<TimeoutOperationType>._, A<TimeoutContext?>._))
			.MustHaveHappened();
		A.CallTo(() => timePolicy.GetTimeoutFor(A<TimeoutOperationType>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_ContinuesAfterException()
	{
		// Arrange -- first poll throws, every subsequent poll succeeds. Signal the moment the
		// service polls AGAIN after the exception (n >= 2), which deterministically proves the
		// loop recovered. (ADR-213 recipe: no short auto-expiring CTS run-window, no Task.Delay
		// poll -- both flake under CI thread-pool starvation.)
		var callCount = 0;
		var recoveredAfterException = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var n = Interlocked.Increment(ref callCount);
				if (n == 1) throw new InvalidOperationException("Transient error");
				_ = recoveredAfterException.TrySetResult();
				return Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>());
			});

		var service = CreateService(store: store);

		// Act -- non-expiring start token; StopAsync alone drives shutdown. (A short-lived start
		// token links into the BackgroundService stopping token and can cancel the loop before it
		// recovers under CI load -- the exact flake this refactor removes.)
		await service.StartAsync(CancellationToken.None);
		try
		{
			// Surface the loop's own fault, exactly as ExecuteAsync_WithNoTimePolicy_ProcessesNormally
			// does. THIS is the test that failed on Windows CI, and it was the one that could not say
			// why: it reported only "Timed out waiting for signal after 00:00:30" against a 50ms poll
			// interval -- roughly 600 missed cycles, which is not slowness. BackgroundService swallows an
			// ExecuteAsync fault into ExecuteTask, so a loop that died on or before its first poll is
			// indistinguishable from one that is merely slow, and this arm is the likelier place for that
			// to happen because its first poll throws by design. Awaiting ExecuteTask when it finishes
			// first re-throws the real exception. This does NOT widen the timeout and does not make a
			// failing run pass; it turns an unactionable flake into a diagnosable one.
			var loop = service.ExecuteTask;
			if (loop is null)
			{
				throw new InvalidOperationException(
					"ExecuteTask was null after StartAsync: the background loop was never started.");
			}

			var finished = await Task.WhenAny(recoveredAfterException.Task, loop);
			if (!ReferenceEquals(finished, recoveredAfterException.Task))
			{
				// Re-throws the real fault if ExecuteAsync threw.
				await loop;

				throw new InvalidOperationException(
					"The scheduler loop ended without polling the store a second time. ExecuteAsync "
					+ $"completed without faulting after {Volatile.Read(ref callCount)} poll(s), so the "
					+ "transient exception ended the loop rather than being absorbed by it.");
			}

			await WaitHelpers.AwaitSignalAsync(recoveredAfterException.Task, TimeSpan.FromSeconds(30));
		}
		finally
		{
			await service.StopAsync(CancellationToken.None);
		}

		// Assert -- the second poll fired only AFTER the first-call exception, so awaiting its
		// signal proves continues-after-exception deterministically (stronger than a bare
		// callCount>1 that a single non-throwing poll could satisfy).
		callCount.ShouldBeGreaterThan(1);
	}
	[Fact]
	public async Task PollOnlyWhenTheInjectedClockReachesTheInterval()
	{
		var clock = new ObservedClock();
		var store = A.Fake<IScheduleStore>();
		var secondPoll = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var calls = 0;
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (Interlocked.Increment(ref calls) == 2) secondPoll.TrySetResult();
				return Task.FromResult<IEnumerable<IScheduledMessage>>([]);
			});
		using var service = CreateService(store: store, timeProvider: clock);
		await service.StartAsync(CancellationToken.None);
		try
		{
			await clock.TimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));
			clock.Advance(TimeSpan.FromMilliseconds(49));
			Volatile.Read(ref calls).ShouldBe(1);
			clock.Advance(TimeSpan.FromMilliseconds(1));
			await secondPoll.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Volatile.Read(ref calls).ShouldBe(2);
		}
		finally
		{
			await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CancelRetrievalByDeadlineOrCaller(bool cancelCaller)
	{
		var clock = new FakeTimeProvider();
		var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
		var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<IScheduleStore>();
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily((CancellationToken token) => RetrieveAsync(token));
		using var caller = new CancellationTokenSource();
		using var service = CreateService(store: store,
			timePolicy: CreateConfigurableTimePolicy(true, TimeSpan.FromSeconds(1)), timeProvider: clock);
		await service.StartAsync(caller.Token);
		try
		{
			var token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			clock.Advance(TimeSpan.FromMilliseconds(999));
			token.IsCancellationRequested.ShouldBeFalse();
			if (cancelCaller) caller.Cancel();
			else clock.Advance(TimeSpan.FromMilliseconds(1));
			await canceled.Task.WaitAsync(TimeSpan.FromSeconds(10));
			token.IsCancellationRequested.ShouldBeTrue();
		}
		finally
		{
			await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		}

		async Task<IEnumerable<IScheduledMessage>> RetrieveAsync(CancellationToken token)
		{
			entered.TrySetResult(token);
			try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				canceled.TrySetResult();
				throw;
			}
			return [];
		}
	}

	[Fact]
	public async Task TreatZeroDeadlineAsAlreadyExpired()
	{
		var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<IScheduleStore>();
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily((CancellationToken token) =>
			{
				entered.TrySetResult(token);
				return Task.FromResult<IEnumerable<IScheduledMessage>>([]);
			});
		using var service = CreateService(store: store,
			timePolicy: CreateConfigurableTimePolicy(true, TimeSpan.Zero), timeProvider: new FakeTimeProvider());
		await service.StartAsync(CancellationToken.None);
		try
		{
			(await entered.Task.WaitAsync(TimeSpan.FromSeconds(10))).IsCancellationRequested.ShouldBeTrue();
		}
		finally { await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); }
	}

	[Fact]
	public async Task CancelTheActualSignalReadThenDeliverAFreshSignalToTheNextWait()
	{
		var clock = new FakeTimeProvider();
		var actual = new InMemoryScheduleStore();
		var store = A.Fake<IScheduleStore>(options => options.Implements<IScheduleStoreSignal>());
		var waits = new System.Collections.Concurrent.ConcurrentQueue<TaskCompletionSource>();
		var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var third = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		waits.Enqueue(first); waits.Enqueue(second); waits.Enqueue(third);
		var polls = 0;
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._)).ReturnsLazily(() =>
		{
			Interlocked.Increment(ref polls);
			return Task.FromResult<IEnumerable<IScheduledMessage>>([]);
		});
		A.CallTo(() => ((IScheduleStoreSignal)store).WaitForChangeAsync(A<TimeSpan>._, A<CancellationToken>._))
			.ReturnsLazily((TimeSpan timeout, CancellationToken token) =>
			{
				timeout.ShouldBe(Timeout.InfiniteTimeSpan);
				var pending = actual.WaitForChangeAsync(timeout, token);
				if (waits.TryDequeue(out var entered)) entered.TrySetResult();
				return pending;
			});
		using var service = CreateService(store: store, timeProvider: clock);
		await service.StartAsync(CancellationToken.None);
		try
		{
			await first.Task.WaitAsync(TimeSpan.FromSeconds(10));
			clock.Advance(TimeSpan.FromMilliseconds(49));
			Volatile.Read(ref polls).ShouldBe(1);
			clock.Advance(TimeSpan.FromMilliseconds(1));
			await second.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Volatile.Read(ref polls).ShouldBe(2);
			await actual.StoreAsync(new ScheduledMessage { Id = Guid.NewGuid() }, CancellationToken.None);
			await third.Task.WaitAsync(TimeSpan.FromSeconds(10));
			Volatile.Read(ref polls).ShouldBe(3);
		}
		finally { await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); }
	}

	[Fact]
	public async Task KeepAnActiveInfinitePolicyLinkedOnlyToTheCaller()
	{
		var clock = new FakeTimeProvider();
		var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<IScheduleStore>();
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily((CancellationToken token) => RetrieveAsync(token));
		using var caller = new CancellationTokenSource();
		using var service = CreateService(store: store,
			timePolicy: CreateConfigurableTimePolicy(true, Timeout.InfiniteTimeSpan), timeProvider: clock);
		await service.StartAsync(caller.Token);
		try
		{
			var token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			clock.Advance(TimeSpan.FromDays(1));
			token.IsCancellationRequested.ShouldBeFalse();
			caller.Cancel();
			token.IsCancellationRequested.ShouldBeTrue();
		}
		finally { await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); }

		async Task<IEnumerable<IScheduledMessage>> RetrieveAsync(CancellationToken token)
		{
			entered.TrySetResult(token);
			await Task.Delay(Timeout.InfiniteTimeSpan, token);
			return [];
		}
	}

	[Fact]
	public async Task DisposeSuccessfulRetrievalDeadlineBeforeTheNextPoll()
	{
		var clock = new ObservedClock();
		var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<IScheduleStore>();
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily((CancellationToken token) =>
			{
				entered.TrySetResult(token);
				return Task.FromResult<IEnumerable<IScheduledMessage>>([]);
			});
		using var service = CreateService(store: store,
			timePolicy: CreateConfigurableTimePolicy(true, TimeSpan.FromSeconds(1)), timeProvider: clock);
		await service.StartAsync(CancellationToken.None);
		try
		{
			var completedToken = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			// The polling timer is created only after the completed retrieval scope has been disposed.
			await clock.PollTimerCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));
			completedToken.IsCancellationRequested.ShouldBeFalse();
			clock.Advance(TimeSpan.FromSeconds(2));
			completedToken.IsCancellationRequested.ShouldBeFalse("the disposed deadline must not cancel a completed operation later");
		}
		finally { await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); }
	}

	private sealed class ObservedClock : TimeProvider
	{
		private readonly FakeTimeProvider _clock = new();
		public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public TaskCompletionSource PollTimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
		public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();
		public override long GetTimestamp() => _clock.GetTimestamp();
		public override long TimestampFrequency => _clock.TimestampFrequency;
		public void Advance(TimeSpan elapsed) => _clock.Advance(elapsed);
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			var timer = _clock.CreateTimer(callback, state, dueTime, period);
			TimerCreated.TrySetResult();
			if (dueTime == TimeSpan.FromMilliseconds(50)) PollTimerCreated.TrySetResult();
			return timer;
		}
	}

}
