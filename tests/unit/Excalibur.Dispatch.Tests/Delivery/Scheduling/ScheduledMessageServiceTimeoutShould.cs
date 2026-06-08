// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

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
public sealed class ScheduledMessageServiceTimeoutShould
{
	private static ScheduledMessageService CreateService(
		IScheduleStore? store = null,
		ITimePolicy? timePolicy = null,
		ITimeoutMonitor? timeoutMonitor = null,
		ILogger<ScheduledMessageService>? logger = null)
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
			timePolicy,
			timeoutMonitor);
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
		// Arrange
		var callCount = 0;
		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var n = Interlocked.Increment(ref callCount);
				if (n == 1) throw new InvalidOperationException("Transient error");
				return Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>());
			});

		var service = CreateService(store: store);

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await service.StartAsync(cts.Token);

		// Poll until the service has been called more than once (recovery after exception)
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (Volatile.Read(ref callCount) <= 1 && DateTime.UtcNow < deadline)
		{
			await Task.Delay(100);
		}

		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert
		callCount.ShouldBeGreaterThan(1);
	}
}