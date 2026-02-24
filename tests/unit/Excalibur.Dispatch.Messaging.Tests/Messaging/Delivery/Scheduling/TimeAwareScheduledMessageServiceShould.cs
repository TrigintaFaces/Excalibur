// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", TestComponents.Messaging)]
public sealed class TimeAwareScheduledMessageServiceShould
{
	[Fact]
	public async Task ProcessDueActionMessageAndPersistUpdatedSchedule()
	{
		var schedule = CreateDueSchedule(typeof(TestActionMessage).Name, interval: TimeSpan.FromMinutes(5));
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = A.Fake<IJsonSerializer>();
		_ = A.CallTo(() => serializer.DeserializeAsync(A<string>._, A<Type>._))
			.ReturnsLazily((string _, Type _) => Task.FromResult<object?>(new TestActionMessage()));

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

		var timeoutMonitor = new RecordingTimeoutMonitor();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy(), timeoutMonitor);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var processed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => store.StoreCalls >= 1, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(25))
			.ConfigureAwait(false);
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
		capturedContext.TraceParent.ShouldBe(schedule.TraceParent);
		capturedContext.TenantId.ShouldBe(schedule.TenantId);
		capturedContext.UserId.ShouldBe(schedule.UserId);

		timeoutMonitor.CompletedOperations.ShouldNotBeEmpty();
		timeoutMonitor.CompletedOperations[0].Success.ShouldBeTrue();
		timeoutMonitor.CompletedOperations[0].TimedOut.ShouldBeFalse();
	}

	[Fact]
	public async Task DisableOneTimeScheduleAfterSuccessfulDispatch()
	{
		var schedule = CreateDueSchedule(typeof(TestActionMessage).Name, interval: null);
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = A.Fake<IJsonSerializer>();
		_ = A.CallTo(() => serializer.DeserializeAsync(A<string>._, A<Type>._))
			.ReturnsLazily((string _, Type _) => Task.FromResult<object?>(new TestActionMessage()));

		var dispatcher = A.Fake<IDispatcher>();
		_ = A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(MessageResult.Success());

		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy(), new RecordingTimeoutMonitor());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var processed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => store.StoreCalls >= 1, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(25))
			.ConfigureAwait(false);
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
		var schedule = CreateDueSchedule(typeof(TestActionMessage).Name, interval: TimeSpan.FromMinutes(1));
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = A.Fake<IJsonSerializer>();
		_ = A.CallTo(() => serializer.DeserializeAsync(A<string>._, A<Type>._))
			.ReturnsLazily((string _, Type _) => Task.FromResult<object?>(null));

		var dispatcher = A.Fake<IDispatcher>();
		var timeoutMonitor = new RecordingTimeoutMonitor();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy(), timeoutMonitor);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var observed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => timeoutMonitor.CompletedOperations.Count >= 1, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(25))
			.ConfigureAwait(false);
		observed.ShouldBeTrue();
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
		store.StoreCalls.ShouldBe(0);
		timeoutMonitor.CompletedOperations[0].Success.ShouldBeFalse();
		timeoutMonitor.CompletedOperations[0].TimedOut.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleUnknownMessageTypeWithoutNullTimeoutTokens()
	{
		var schedule = CreateDueSchedule("NonExistent.Message.Type, MissingAssembly", interval: TimeSpan.FromMinutes(1));
		var store = new SequenceScheduleStore([schedule], []);
		var serializer = A.Fake<IJsonSerializer>();
		var dispatcher = A.Fake<IDispatcher>();
		var timeoutMonitor = new RecordingTimeoutMonitor();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy(), timeoutMonitor);

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		var observed = await global::Tests.Shared.Infrastructure.WaitHelpers
			.WaitUntilAsync(() => timeoutMonitor.CompletedOperations.Count >= 1, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(25))
			.ConfigureAwait(false);
		observed.ShouldBeTrue();
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		timeoutMonitor.StartedOperationCount.ShouldBeGreaterThanOrEqualTo(1);
		timeoutMonitor.CompletedOperations.ShouldNotBeEmpty();
		timeoutMonitor.CompletedOperations.All(static completion => completion.TokenWasNull == false).ShouldBeTrue();
		A.CallTo(() => serializer.DeserializeAsync(A<string>._, A<Type>._)).MustNotHaveHappened();
		A.CallTo(() => dispatcher.DispatchAsync(A<IDispatchAction>._, A<IMessageContext>._, A<CancellationToken>._)).MustNotHaveHappened();
		store.StoreCalls.ShouldBe(0);
	}

	[Fact]
	public async Task DisposeAsyncStoreDuringStop()
	{
		var store = new SequenceScheduleStore([]);
		var serializer = A.Fake<IJsonSerializer>();
		var dispatcher = A.Fake<IDispatcher>();
		using var sut = CreateService(store, dispatcher, serializer, new NoTimeoutPolicy(), new RecordingTimeoutMonitor());

		await sut.StartAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50).ConfigureAwait(false);
		await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);

		store.AsyncDisposed.ShouldBeTrue();
	}

	private static TimeAwareScheduledMessageService CreateService(
		SequenceScheduleStore store,
		IDispatcher dispatcher,
		IJsonSerializer serializer,
		ITimePolicy timePolicy,
		ITimeoutMonitor timeoutMonitor) =>
		new(
			store,
			dispatcher,
			serializer,
			timePolicy,
			timeoutMonitor,
			Microsoft.Extensions.Options.Options.Create(new SchedulerOptions
			{
				PollInterval = TimeSpan.FromMilliseconds(20),
			}),
			NullLogger<TimeAwareScheduledMessageService>.Instance);

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
			NextExecutionUtc = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromSeconds(1)),
		};

	private sealed class SequenceScheduleStore(params IEnumerable<IScheduledMessage>[] batches) : IScheduleStore, IAsyncDisposable
	{
		private readonly ConcurrentQueue<IEnumerable<IScheduledMessage>> _batches = new(batches);
		private int _storeCalls;
		private int _isDisposed;

		public List<ScheduledMessage> StoredMessages { get; } = [];

		public int StoreCalls => Volatile.Read(ref _storeCalls);

		public bool AsyncDisposed => Volatile.Read(ref _isDisposed) == 1;

		public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (_batches.TryDequeue(out var next))
			{
				return Task.FromResult(next);
			}

			return Task.FromResult<IEnumerable<IScheduledMessage>>([]);
		}

		public Task StoreAsync(IScheduledMessage message, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_ = Interlocked.Increment(ref _storeCalls);
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

	private sealed class RecordingTimeoutMonitor : ITimeoutMonitor
	{
		private readonly ConcurrentQueue<CompletionRecord> _completed = [];
		private int _startedOperationCount;

		public int StartedOperationCount => Volatile.Read(ref _startedOperationCount);

		public IReadOnlyList<CompletionRecord> CompletedOperations => [.. _completed];

		public ITimeoutOperationToken StartOperation(TimeoutOperationType operationType, TimeoutContext? context = null)
		{
			_ = Interlocked.Increment(ref _startedOperationCount);
			return new TestTimeoutOperationToken(operationType, context);
		}

		public void CompleteOperation(ITimeoutOperationToken token, bool success, bool timedOut) =>
			_completed.Enqueue(new CompletionRecord(token is null, success, timedOut));

		public TimeoutStatistics GetStatistics(TimeoutOperationType operationType) =>
			new()
			{
				OperationType = operationType,
				TotalOperations = _completed.Count,
			};

		public TimeSpan GetRecommendedTimeout(TimeoutOperationType operationType, int percentile = 95, TimeoutContext? context = null) =>
			TimeSpan.FromSeconds(1);

		public void ClearStatistics(TimeoutOperationType? operationType = null)
		{
		}

		public int GetSampleCount(TimeoutOperationType operationType) => _completed.Count;

		public bool HasSufficientSamples(TimeoutOperationType operationType, int minimumSamples = 100) =>
			_completed.Count >= minimumSamples;
	}

	private readonly record struct CompletionRecord(bool TokenWasNull, bool Success, bool TimedOut);

	private sealed class TestTimeoutOperationToken(TimeoutOperationType operationType, TimeoutContext? context)
		: ITimeoutOperationToken
	{
		public Guid OperationId { get; } = Guid.NewGuid();

		public TimeoutOperationType OperationType { get; } = operationType;

		public TimeoutContext? Context { get; } = context;

		public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

		public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartTime;

		public bool IsCompleted { get; private set; }

		public bool? IsSuccessful { get; private set; }

		public bool? HasTimedOut { get; private set; }

		public void Dispose() => IsCompleted = true;
	}

	private sealed class TestActionMessage : IDispatchAction;
}
