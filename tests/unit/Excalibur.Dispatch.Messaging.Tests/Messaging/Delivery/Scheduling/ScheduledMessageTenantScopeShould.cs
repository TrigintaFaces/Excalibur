// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.MessageResult;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
/// A scheduled message carries the tenant it was scheduled for. Stamping that tenant onto the dispatch
/// context is not enough on its own: an <c>ITenantContext</c>-reading store reads the ambient
/// <see cref="TenantContextHolder"/>, not the message context, so the tenant has to be *established*
/// for the dispatch, not merely recorded on it.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Messaging)]
public sealed class ScheduledMessageTenantScopeShould
{
	private static readonly TimeSpan ObservationTimeout = TimeSpan.FromSeconds(30);

	[Fact]
	public async Task EstablishTheSchedulesOwnTenantAsAmbientForTheDispatch()
	{
		// Arrange — the poller runs under a DIFFERENT ambient tenant than the schedule names. Anything
		// that merely inherits the caller's scope will observe the poller's tenant, not the schedule's.
		MessageTypeRegistry.RegisterType<TenantScopedTestAction>();
		var schedule = CreateDueSchedule("tenant-a");
		var store = new AmbientRecordingScheduleStore(schedule);

		string? ambientDuringDispatch = null;
		var dispatched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() =>
			{
				ambientDuringDispatch = TenantContextHolder.Current;
				_ = dispatched.TrySetResult(true);
			})
			.Returns(MessageResult.Success());

		using var pollerAmbient = TenantContextHolder.BeginScope("poller-ambient-tenant");
		using var sut = CreateService(store, dispatcher);

		// Act
		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var observed = await WaitAsync(dispatched.Task).ConfigureAwait(false);
		var stored = await WaitAsync(store.StoreObserved).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		observed.ShouldBeTrue("the due schedule was never dispatched");
		stored.ShouldBeTrue("the schedule was never written back");

		// Assert — liveness: the handler runs under the tenant the message was scheduled for, so an
		// ITenantContext-reading store it touches attributes its writes to that tenant.
		ambientDuringDispatch.ShouldBe("tenant-a");

		// Assert — safety: it is NOT the poller's ambient tenant. Without this arm the liveness
		// assertion alone would also pass for an implementation that simply inherited the caller.
		ambientDuringDispatch.ShouldNotBe("poller-ambient-tenant");

		// Assert — the scope is closed again, so the message's tenant does not leak onto the poller's own
		// bookkeeping write, nor onto the next schedule in the batch.
		store.AmbientDuringStore.ShouldBe("poller-ambient-tenant");
		TenantContextHolder.Current.ShouldBe("poller-ambient-tenant");
	}

	private static async Task<bool> WaitAsync(Task<bool> signal)
	{
		try
		{
			return await signal.WaitAsync(ObservationTimeout).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			return false;
		}
	}

	private static ScheduledMessageService CreateService(IScheduleStore store, IDispatcher dispatcher) =>
		new(
			store,
			dispatcher,
			new DispatchJsonSerializer(),
			A.Fake<ICronScheduler>(),
			MsOptions.Create(new SchedulerOptions { PollInterval = TimeSpan.FromMilliseconds(20) }),
			MsOptions.Create(new CronScheduleOptions()),
			NullLogger<ScheduledMessageService>.Instance);

	private static ScheduledMessage CreateDueSchedule(string tenantId) =>
		new()
		{
			Id = Guid.NewGuid(),
			Enabled = true,
			CronExpression = string.Empty,
			Interval = TimeSpan.FromMinutes(5),
			MessageName = typeof(TenantScopedTestAction).AssemblyQualifiedName!,
			MessageBody = "{}",
			CorrelationId = "corr-tenant-scope",
			TenantId = tenantId,
			UserId = "user-1",
			// Comfortably in the past so a runner clock that jitters backwards cannot make it not-due.
			NextExecutionUtc = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(1)),
		};

	/// <summary>
	/// Records the ambient tenant observed on the store write that follows the dispatch, which is the
	/// poller's own bookkeeping and must not inherit the dispatched message's tenant.
	/// </summary>
	private sealed class AmbientRecordingScheduleStore(IScheduledMessage schedule) : IScheduleStore
	{
		private readonly Queue<IScheduledMessage> _pending = new([schedule]);

		public TaskCompletionSource<bool> StoreObservedSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task<bool> StoreObserved => StoreObservedSource.Task;

		public string? AmbientDuringStore { get; private set; }

		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			lock (_pending)
			{
				return Task.FromResult<IEnumerable<IScheduledMessage>>(
					_pending.Count > 0 ? [_pending.Dequeue()] : []);
			}
		}

		public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			AmbientDuringStore = TenantContextHolder.Current;
			_ = StoreObservedSource.TrySetResult(true);
			return Task.CompletedTask;
		}

		public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.CompletedTask;
		}
	}

	private sealed class TenantScopedTestAction : IDispatchAction;
}
