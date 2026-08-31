// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
/// Tests that <see cref="ScheduledMessageService"/> correctly integrates optional
/// <see cref="ITimePolicy"/> for timeout-aware scheduling. The unified service
/// replaced the former TimeAwareScheduledMessageService.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Messaging)]
public sealed class ScheduledMessageServiceTimeAwareShould
{
	private static readonly TimeSpan ScheduleProcessingTimeout = TimeSpan.FromSeconds(30);

	[Fact]
	public async Task ProcessDueActionMessageAndPersistUpdatedSchedule()
	{
		var schedule = CreateDueSchedule(typeof(TestActionMessage), interval: TimeSpan.FromMinutes(5));
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		var dispatchCount = 0;
		IMessageContext? capturedContext = null;
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes((IDispatchAction _, IMessageContext context, CancellationToken _) =>
			{
				_ = Interlocked.Increment(ref dispatchCount);
				capturedContext = context;
			})
			.Returns(MessageResult.Success());

		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var processed = await store.WaitForStoreCallAsync(ScheduleProcessingTimeout, CancellationToken.None).ConfigureAwait(false);
		processed.ShouldBeTrue();
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		dispatchCount.ShouldBeGreaterThanOrEqualTo(1);
		store.StoredMessages.Count.ShouldBeGreaterThanOrEqualTo(1);

		var updated = store.StoredMessages[0];
		updated.Enabled.ShouldBeTrue();
		updated.LastExecutionUtc.ShouldNotBeNull();
		updated.NextExecutionUtc.ShouldNotBeNull();

		_ = capturedContext.ShouldNotBeNull();
		capturedContext.CorrelationId.ShouldBe(schedule.CorrelationId);
		capturedContext.GetTraceParent().ShouldBe(schedule.TraceParent);
		capturedContext.GetTenantId().ShouldBe(schedule.TenantId);
		capturedContext.GetUserId().ShouldBe(schedule.UserId);
	}

	[Fact]
	public async Task DisableOneTimeScheduleAfterSuccessfulDispatch()
	{
		var schedule = CreateDueSchedule(typeof(TestActionMessage), interval: null);
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());

		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var processed = await store.WaitForStoreCallAsync(ScheduleProcessingTimeout, CancellationToken.None).ConfigureAwait(false);
		processed.ShouldBeTrue();
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		store.StoredMessages.Count.ShouldBeGreaterThanOrEqualTo(1);
		var updated = store.StoredMessages[0];
		updated.Enabled.ShouldBeFalse();
		updated.LastExecutionUtc.ShouldNotBeNull();
	}

	[Fact]
	public async Task SkipDispatchButStillAdvanceWhenDeserializationFails()
	{
		// Use malformed JSON that will cause JsonSerializer.Deserialize to throw JsonException.
		var schedule = CreateDueSchedule(typeof(TestActionMessage), interval: TimeSpan.FromMinutes(1));
		schedule.MessageBody = "<<invalid-json>>";
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		// Wait for the advance itself -- the batch being dequeued only means the loop READ the row, not that it
		// finished handling the failure, so waiting on that and then sleeping races the subject on a loaded runner.
		var advanced = await store.WaitForStoreCallAsync(ScheduleProcessingTimeout, CancellationToken.None).ConfigureAwait(false);
		advanced.ShouldBeTrue();
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		// SAFETY: an undeserializable body is never dispatched.
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();

		// LIVENESS: it IS persisted, because the row must still advance. This assertion was previously
		// ShouldBe(0) -- asserting the row was left untouched, which is exactly why a body that can never
		// deserialize stayed due and was re-processed and re-logged on every poll for the life of the
		// process. Advancing is the fix, so the write is the observable evidence of it.
		store.StoreCalls.ShouldBeGreaterThan(
			0,
			"a row that failed must advance, or it is still due on the very next poll");
		schedule.Enabled.ShouldBeTrue("an interval schedule can still be advanced, so it keeps running");
		schedule.NextExecutionUtc.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleUnknownMessageTypeGracefully()
	{
		var schedule = CreateDueSchedule("NonExistent.Message.Type, MissingAssembly", interval: TimeSpan.FromMinutes(1));
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = new DispatchJsonSerializer();
		var dispatcher = A.Fake<IDispatcher>();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var advanced = await store.WaitForStoreCallAsync(ScheduleProcessingTimeout, CancellationToken.None).ConfigureAwait(false);
		advanced.ShouldBeTrue();
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._)).MustNotHaveHappened();

		// Graceful means the row is skipped AND advanced. Leaving its next-execution time untouched -- which is
		// what a store call count of zero would mean -- makes the same unresolvable row due again on every poll
		// forever, ahead of every other schedule.
		store.StoreCalls.ShouldBe(1);
		var stored = store.StoredMessages.ShouldHaveSingleItem();
		stored.NextExecutionUtc.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task DisposeAsyncStoreDuringStop()
	{
		var store = new SequenceScheduleStore([]);
		var serializer = new DispatchJsonSerializer();
		var dispatcher = A.Fake<IDispatcher>();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		store.AsyncDisposed.ShouldBeTrue();
	}

	[Fact]
	public async Task KeepProcessingLaterSchedulesWhenAnEarlierRowHasNoResolvableType()
	{
		// Durable schedule rows outlive the code that created them, so a row naming a type this process no
		// longer has is the expected steady state, not an exceptional one. It must cost that one row, never
		// the scan: aborting the loop leaves every later row undispatched and its next-execution time
		// unadvanced, so the same poisoned row is still first on the next poll and the starvation is permanent.
		var poisoned = CreateDueSchedule("Excalibur.Dispatch.Tests.NoSuchScheduledMessageTypeExists", TimeSpan.FromMinutes(5));
		var healthy = CreateDueSchedule(typeof(TestActionMessage), interval: TimeSpan.FromMinutes(5));
		var store = new SequenceScheduleStore(new List<IScheduledMessage> { poisoned, healthy });
		var serializer = new DispatchJsonSerializer();

		var dispatched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => dispatched.TrySetResult(true))
			.Returns(MessageResult.Success());

		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var healthyRan = await WaitForAsync(dispatched.Task).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		healthyRan.ShouldBeTrue();
	}

	[Fact]
	public async Task KeepProcessingLaterSchedulesWhenAnEarlierRowFailsToDeserialize()
	{
		var poisoned = CreateDueSchedule(typeof(TestActionMessage), interval: TimeSpan.FromMinutes(5));
		poisoned.MessageBody = "<<invalid-json>>";
		var healthy = CreateDueSchedule(typeof(TestActionMessage), interval: TimeSpan.FromMinutes(5));
		var store = new SequenceScheduleStore(new List<IScheduledMessage> { poisoned, healthy });
		var serializer = new DispatchJsonSerializer();

		var dispatched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.Invokes(() => dispatched.TrySetResult(true))
			.Returns(MessageResult.Success());

		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var healthyRan = await WaitForAsync(dispatched.Task).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		healthyRan.ShouldBeTrue();
	}

	private static async Task<bool> WaitForAsync(Task<bool> signal)
	{
		try
		{
			return await signal.WaitAsync(ScheduleProcessingTimeout).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			return false;
		}
	}

	private static ScheduledMessageService CreateService(
		SequenceScheduleStore store,
		IDispatcher dispatcher,
		DispatchJsonSerializer serializer,
		ITimePolicy timePolicy) =>
		new(
			store,
			dispatcher,
			serializer,
			A.Fake<ICronScheduler>(),
			Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
			{
				PollInterval = TimeSpan.FromMilliseconds(20),
			}),
			Microsoft.Extensions.Options.Options.Create(new CronScheduleOptions()),
			NullLogger<ScheduledMessageService>.Instance,
			timePolicy);

	private static ScheduledMessage CreateDueSchedule(Type messageType, TimeSpan? interval)
	{
		MessageTypeRegistry.RegisterType(messageType);
		var registeredTypeName = messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name;
		return CreateDueSchedule(registeredTypeName, interval);
	}

	private static ScheduledMessage CreateDueSchedule(string messageTypeName, TimeSpan? interval) =>
		new()
		{
			Id = Guid.NewGuid(),
			Enabled = true,
			CronExpression = string.Empty,
			Interval = interval,
			MessageName = messageTypeName,
			MessageBody = "{}",
			CorrelationId = "corr-123",
			TraceParent = "trace-123",
			TenantId = "tenant-a",
			UserId = "user-42",
			// Keep this comfortably in the past to avoid false negatives when CI runner clocks jitter backwards.
			NextExecutionUtc = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(1)),
		};

	private sealed class SequenceScheduleStore(params IEnumerable<IScheduledMessage>[] batches) : IScheduleStore, IAsyncDisposable
	{
		private readonly ConcurrentQueue<IEnumerable<IScheduledMessage>> _batches = new(batches);
		private readonly TaskCompletionSource<bool> _storeCallObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _storeCalls;
		private int _isDisposed;

		public List<ScheduledMessage> StoredMessages { get; } = [];

		public int StoreCalls => Volatile.Read(ref _storeCalls);

		public bool AsyncDisposed => Volatile.Read(ref _isDisposed) == 1;

		public async Task<bool> WaitForStoreCallAsync(TimeSpan timeout, CancellationToken cancellationToken)
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(timeout);

			try
			{
				_ = await _storeCallObserved.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
				return true;
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				return false;
			}
		}

		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_batches.TryDequeue(out var next))
			{
				return Task.FromResult<IEnumerable<IScheduledMessage>>(next.ToList());
			}

			return Task.FromResult<IEnumerable<IScheduledMessage>>([]);
		}

		public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_ = Interlocked.Increment(ref _storeCalls);
			_ = _storeCallObserved.TrySetResult(true);
			if (message is ScheduledMessage scheduled)
			{
				StoredMessages.Add(CloneScheduledMessage(scheduled));
			}

			return Task.CompletedTask;
		}

		public Task CompleteAsync(Guid scheduleId, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.CompletedTask;
		}

		public ValueTask DisposeAsync()
		{
			_ = Interlocked.Exchange(ref _isDisposed, 1);
			return ValueTask.CompletedTask;
		}

		private static ScheduledMessage CloneScheduledMessage(ScheduledMessage source) =>
			new()
			{
				Id = source.Id,
				Enabled = source.Enabled,
				CronExpression = source.CronExpression,
				Interval = source.Interval,
				MessageName = source.MessageName,
				MessageBody = source.MessageBody,
				CorrelationId = source.CorrelationId,
				TraceParent = source.TraceParent,
				TenantId = source.TenantId,
				UserId = source.UserId,
				NextExecutionUtc = source.NextExecutionUtc,
				LastExecutionUtc = source.LastExecutionUtc,
				TimeZoneId = source.TimeZoneId,
				MissedExecutionBehavior = source.MissedExecutionBehavior,
			};
	}

	private sealed class NoTimeoutPolicy : ITimePolicy
	{
		public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(10);

		public TimeSpan MaxTimeout => TimeSpan.FromMinutes(5);

		public TimeSpan HandlerTimeout => TimeSpan.FromSeconds(10);

		public TimeSpan SerializationTimeout => TimeSpan.FromSeconds(10);

		public TimeSpan TransportTimeout => TimeSpan.FromSeconds(10);

		public TimeSpan ValidationTimeout => TimeSpan.FromSeconds(10);

		public TimeSpan GetTimeoutFor(TimeoutOperationType operationType) => TimeSpan.FromSeconds(10);

		public bool ShouldApplyTimeout(TimeoutOperationType operationType, TimeoutContext? context = null) => false;

		public CancellationToken CreateTimeoutToken(TimeoutOperationType operationType, CancellationToken parentToken) => parentToken;
	}

	private sealed class TestActionMessage : IDispatchAction;
}