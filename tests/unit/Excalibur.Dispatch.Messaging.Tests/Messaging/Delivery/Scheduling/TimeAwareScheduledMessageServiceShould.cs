// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Options.Scheduling;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

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
	public async Task SkipDispatchAndPersistenceWhenDeserializationFails()
	{
		// Use malformed JSON that will cause JsonSerializer.Deserialize to throw JsonException.
		var schedule = CreateDueSchedule(typeof(TestActionMessage), interval: TimeSpan.FromMinutes(1));
		schedule.MessageBody = "<<invalid-json>>";
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = new DispatchJsonSerializer();

		var dispatcher = A.Fake<IDispatcher>();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		// Wait for the batch to be consumed (processing loop picks up the schedule)
		var consumed = await store.WaitForBatchConsumedAsync(ScheduleProcessingTimeout, CancellationToken.None).ConfigureAwait(false);
		consumed.ShouldBeTrue();
		// Allow one more poll cycle so error handling completes
		await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		store.StoreCalls.ShouldBe(0);
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
		var consumed = await store.WaitForBatchConsumedAsync(ScheduleProcessingTimeout, CancellationToken.None).ConfigureAwait(false);
		consumed.ShouldBeTrue();
		await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._)).MustNotHaveHappened();
		store.StoreCalls.ShouldBe(0);
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
		private readonly TaskCompletionSource<bool> _batchConsumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

		public async Task<bool> WaitForBatchConsumedAsync(TimeSpan timeout, CancellationToken cancellationToken)
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(timeout);

			try
			{
				_ = await _batchConsumed.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
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
				var items = next.ToList();
				if (items.Count > 0)
				{
					_ = _batchConsumed.TrySetResult(true);
				}

				return Task.FromResult<IEnumerable<IScheduledMessage>>(items);
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
