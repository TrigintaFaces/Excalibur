// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Delivery.Scheduling;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class MissedScheduleTimeoutShould
{
	[Theory]
	[InlineData(MissedExecutionBehavior.ExecuteLatestMissed)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed)]
	public async Task CancelABlockedReplayAndProcessTheFollowingHealthyRow(MissedExecutionBehavior behavior)
	{
		await VerifyReplayAsync(behavior, applyTimeout: true);
	}

	[Theory]
	[InlineData(MissedExecutionBehavior.ExecuteLatestMissed)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed)]
	public async Task LeaveAnUntimedReplayActiveUntilHostShutdown(MissedExecutionBehavior behavior)
	{
		await VerifyReplayAsync(behavior, applyTimeout: false);
	}

	private static async Task VerifyReplayAsync(MissedExecutionBehavior behavior, bool applyTimeout)
	{
		var now = new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var clock = new FakeTimeProvider(now);
		var serializer = new DispatchJsonSerializer();
		MessageTypeRegistry.RegisterType<TimeoutAction>();
		var overdue = await CreateRowAsync("overdue", now.AddMinutes(-3));
		overdue.LastExecutionUtc = now.AddMinutes(-5);
		overdue.CronExpression = "* * * * *";
		overdue.MissedExecutionBehavior = behavior;
		var healthy = await CreateRowAsync("healthy", now);
		var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var healthyAdvanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var healthyCalls = 0;
		var overdueWrites = 0;
		var writtenOverdue = new ConcurrentQueue<(DateTimeOffset? Last, DateTimeOffset? Next)>();
		var store = A.Fake<IScheduleStore>();
		A.CallTo(() => store.GetAllAsync(A<CancellationToken>._))
			.Returns(Task.FromResult<IEnumerable<IScheduledMessage>>([overdue, healthy]));
		A.CallTo(() => store.StoreAsync(A<IScheduledMessage>._, A<CancellationToken>._))
			.Invokes((IScheduledMessage row, CancellationToken _) =>
			{
				if (row.Id == overdue.Id)
				{
					Interlocked.Increment(ref overdueWrites);
					writtenOverdue.Enqueue((row.LastExecutionUtc, row.NextExecutionUtc));
				}
				if (row.Id == healthy.Id && row.NextExecutionUtc > now)
				{
					healthyAdvanced.TrySetResult();
				}
			}).Returns(Task.CompletedTask);
		var dispatcher = A.Fake<IDispatcher>();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.ReturnsLazily((IDispatchAction action, IMessageContext _, CancellationToken token) => DispatchAsync(action, token));
		var policy = A.Fake<ITimePolicy>(options => options.Implements<ITimePolicyConfiguration>());
		A.CallTo(() => ((ITimePolicyConfiguration)policy).ShouldApplyTimeout(A<TimeoutOperationType>._, A<TimeoutContext?>._))
			.ReturnsLazily((TimeoutOperationType operation, TimeoutContext? _) => applyTimeout && operation == TimeoutOperationType.Handler);
		A.CallTo(() => policy.GetTimeoutFor(TimeoutOperationType.Handler)).Returns(TimeSpan.FromMilliseconds(50));
		var cronOptions = Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions());
		using var scopeProvider = new ServiceCollection().BuildServiceProvider();
		using var service = new ScheduledMessageService(store, dispatcher, serializer,
			new CronScheduler(cronOptions, NullLogger<CronScheduler>.Instance),
			Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
			{
				PollInterval = TimeSpan.FromSeconds(1), EnableAdaptivePolling = false
			}), cronOptions, NullLogger<ScheduledMessageService>.Instance, scopeProvider.GetRequiredService<IServiceScopeFactory>(),
			policy, timeProvider: clock);
		try
		{
			await service.StartAsync(CancellationToken.None);
			await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			clock.Advance(TimeSpan.FromMilliseconds(49));
			canceled.Task.IsCompleted.ShouldBeFalse("the deadline must not fire before its boundary");
			if (applyTimeout)
			{
				clock.Advance(TimeSpan.FromMilliseconds(1));
				var loop = service.ExecuteTask.ShouldNotBeNull();
				var finished = await Task.WhenAny(healthyAdvanced.Task, loop).WaitAsync(TimeSpan.FromSeconds(10));
				if (finished == loop)
				{
					await loop;
					throw new InvalidOperationException("Scheduler ended before the following healthy row advanced.");
				}
				await healthyAdvanced.Task;
				canceled.Task.IsCompletedSuccessfully.ShouldBeTrue();
				Volatile.Read(ref healthyCalls).ShouldBe(1);
				Volatile.Read(ref overdueWrites).ShouldBe(1);
				var persisted = writtenOverdue.Single();
				persisted.Last.ShouldBe(now.AddMinutes(-5));
				persisted.Next.ShouldNotBeNull().ShouldBeGreaterThan(now);
			}
			else
			{
				clock.Advance(TimeSpan.FromDays(1));
				canceled.Task.IsCompleted.ShouldBeFalse("the inactive policy must not impose a handler deadline");
				Volatile.Read(ref healthyCalls).ShouldBe(0);
				Volatile.Read(ref overdueWrites).ShouldBe(0);
			}
		}
		finally
		{
			await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
			var loop = service.ExecuteTask.ShouldNotBeNull();
			try
			{
				await loop;
			}
			catch (OperationCanceledException) when (loop.IsCanceled)
			{
				// Host shutdown cancels the scheduler loop; other terminal faults propagate.
			}
		}
		canceled.Task.IsCompletedSuccessfully.ShouldBeTrue("host shutdown must cancel an active replay");
		if (!applyTimeout)
		{
			Volatile.Read(ref overdueWrites).ShouldBe(0, "host shutdown must not be persisted as a handler failure");
			Volatile.Read(ref healthyCalls).ShouldBe(0);
		}

		async Task<IMessageResult> DispatchAsync(IDispatchAction action, CancellationToken token)
		{
			if (((TimeoutAction)action).Marker == "healthy")
			{
				Interlocked.Increment(ref healthyCalls);
				return MessageResult.Success();
			}
			entered.TrySetResult();
			try
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, token);
				throw new InvalidOperationException("An infinite cooperative handler completed without cancellation.");
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				canceled.TrySetResult();
				throw;
			}
		}

		async Task<ScheduledMessage> CreateRowAsync(string marker, DateTimeOffset due) => new()
		{
			Id = Guid.NewGuid(), Enabled = true, Interval = TimeSpan.FromMinutes(5),
			NextExecutionUtc = due, MessageName = typeof(TimeoutAction).FullName!,
			MessageBody = await serializer.SerializeAsync(new TimeoutAction(marker), typeof(TimeoutAction))
		};
	}

	private sealed record TimeoutAction(string Marker) : IDispatchAction;
}
