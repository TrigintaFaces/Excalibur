// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
				PollInterval = TimeSpan.FromMilliseconds(50)
			}),
			Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()),
			logger ?? NullLogger<ScheduledMessageService>.Instance,
			timePolicy,
			timeoutMonitor);
	}

	/// <summary>
	/// Creates a fake that implements both ITimePolicy and ITimePolicyConfiguration
	/// so ShouldApplyTimeout can be controlled.
	/// </summary>
	private static ITimePolicy CreateConfigurableTimePolicy(bool shouldApply, TimeSpan timeout)
	{
		// FakeItEasy: create a fake that implements both interfaces
		var policy = A.Fake<ITimePolicy>(o => o.Implements<ITimePolicyConfiguration>());

		_ = A.CallTo(() => ((ITimePolicyConfiguration)policy).ShouldApplyTimeout(
				A<TimeoutOperationType>._, A<TimeoutContext?>._))
			.Returns(shouldApply);

		_ = A.CallTo(() => policy.GetTimeoutFor(A<TimeoutOperationType>._))
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
		// Arrange
		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>()));

		var service = CreateService(store: store, timePolicy: null);

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- store was polled normally
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_WithActiveTimePolicy_AppliesTimeout()
	{
		// Arrange -- policy that says "yes, apply timeout" with generous duration
		var timePolicy = CreateConfigurableTimePolicy(shouldApply: true, timeout: TimeSpan.FromSeconds(30));

		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>()));

		var service = CreateService(store: store, timePolicy: timePolicy);

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		await service.StartAsync(cts.Token);
		await Task.Delay(300);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

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
		// Arrange -- policy that says "no, don't apply timeout"
		var timePolicy = CreateConfigurableTimePolicy(shouldApply: false, timeout: TimeSpan.FromSeconds(5));

		var store = A.Fake<IScheduleStore>();
		_ = A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>(Array.Empty<IScheduledMessage>()));

		var service = CreateService(store: store, timePolicy: timePolicy);

		// Act
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
		await service.StartAsync(cts.Token);
		await Task.Delay(200);
		await cts.CancelAsync();
		await service.StopAsync(CancellationToken.None);

		// Assert -- ShouldApplyTimeout consulted, but GetTimeoutFor NOT called
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
