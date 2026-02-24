// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventStoreLiveSubscriptionDepthShould : IAsyncDisposable
{
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _eventSerializer = A.Fake<IEventSerializer>();
	private readonly EventSubscriptionOptions _options;
	private readonly EventStoreLiveSubscription _sut;

	public EventStoreLiveSubscriptionDepthShould()
	{
		_options = new EventSubscriptionOptions
		{
			PollingInterval = TimeSpan.FromMilliseconds(50),
			MaxBatchSize = 10,
			StartPosition = SubscriptionStartPosition.Beginning,
		};

		_sut = new EventStoreLiveSubscription(
			_eventStore,
			_eventSerializer,
			_options,
			NullLogger<EventStoreLiveSubscription>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void ThrowWhenEventStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(
			null!,
			_eventSerializer,
			_options,
			NullLogger<EventStoreLiveSubscription>.Instance));
	}

	[Fact]
	public void ThrowWhenEventSerializerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(
			_eventStore,
			null!,
			_options,
			NullLogger<EventStoreLiveSubscription>.Instance));
	}

	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(
			_eventStore,
			_eventSerializer,
			null!,
			NullLogger<EventStoreLiveSubscription>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(
			_eventStore,
			_eventSerializer,
			_options,
			null!));
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenStreamIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.SubscribeAsync(null!, _ => Task.CompletedTask, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenStreamIdIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.SubscribeAsync("", _ => Task.CompletedTask, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsyncThrowsWhenHandlerIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SubscribeAsync("stream-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsyncStartsPolling()
	{
		// Arrange
		var firstPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				_ = firstPollObserved.TrySetResult();
				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		using var cts = new CancellationTokenSource();

		// Act
		await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, cts.Token);
		await firstPollObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

		// Assert - polling should call LoadAsync at least once
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", A<long>._, A<CancellationToken>._))
			.MustHaveHappened();
		await _sut.UnsubscribeAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task UnsubscribeAsyncStopsPolling()
	{
		// Arrange
		var firstPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var loadCalls = 0;
		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				var count = Interlocked.Increment(ref loadCalls);
				if (count == 1)
				{
					_ = firstPollObserved.TrySetResult();
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, cts.Token);
		await firstPollObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);

		// Act
		await _sut.UnsubscribeAsync(CancellationToken.None);
		var callsAfterUnsubscribe = Volatile.Read(ref loadCalls);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromMilliseconds(250), CancellationToken.None).ConfigureAwait(false);

		// Assert - no additional polls should occur after unsubscribe completes
		Volatile.Read(ref loadCalls).ShouldBe(callsAfterUnsubscribe);
	}

	[Fact]
	public async Task SubscribeAsyncDeliversEventsToHandler()
	{
		// Arrange
		var storedEvent = new StoredEvent(
			"evt-1", "agg-1", "TestAgg", "TestEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false);

		var domainEvent = A.Fake<IDomainEvent>();
		var callCount = 0;

		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", A<long>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				callCount++;
				return callCount == 1
					? new ValueTask<IReadOnlyList<StoredEvent>>(new[] { storedEvent })
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		A.CallTo(() => _eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);

		var deliveredEvents = new List<IDomainEvent>();
		var deliveredObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var cts = new CancellationTokenSource();

		// Act
		await _sut.SubscribeAsync("stream-1", events =>
		{
			deliveredEvents.AddRange(events);
			if (events.Count > 0)
			{
				_ = deliveredObserved.TrySetResult();
			}
			return Task.CompletedTask;
		}, cts.Token);
		await deliveredObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
		await _sut.UnsubscribeAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		deliveredEvents.Count.ShouldBeGreaterThan(0);
		deliveredEvents.ShouldContain(domainEvent);
	}

	[Fact]
	public async Task SubscribeAsyncHandlesDeserializationErrors()
	{
		// Arrange
		var storedEvent = new StoredEvent(
			"evt-1", "agg-1", "TestAgg", "BadEvent", "data"u8.ToArray(), null, 0, DateTimeOffset.UtcNow, false);

		var loadCalls = 0;
		var secondPollObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", A<long>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var count = Interlocked.Increment(ref loadCalls);
				if (count >= 2)
				{
					_ = secondPollObserved.TrySetResult();
					return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(new[] { storedEvent });
			});

		A.CallTo(() => _eventSerializer.ResolveType("BadEvent")).Throws(new InvalidOperationException("Unknown type"));

		var deliveredEvents = new List<IDomainEvent>();
		using var cts = new CancellationTokenSource();

		// Act - should not throw, deserialization errors are skipped
		await _sut.SubscribeAsync("stream-1", events =>
		{
			deliveredEvents.AddRange(events);
			return Task.CompletedTask;
		}, cts.Token);
		await secondPollObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
		await _sut.UnsubscribeAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert - no events should be delivered (they were all bad)
		deliveredEvents.ShouldBeEmpty();
	}

	[Fact]
	public async Task SubscribeAsyncUsesStartPositionBeginning()
	{
		// Arrange
		_options.StartPosition = SubscriptionStartPosition.Beginning;
		var observedBeginningPosition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily((FakeItEasy.Core.IFakeObjectCall call) =>
			{
				var fromPosition = call.GetArgument<long>(2);
				if (fromPosition == -1L)
				{
					observedBeginningPosition.TrySetResult();
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		using var cts = new CancellationTokenSource();

		// Act
		await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, cts.Token);
		await observedBeginningPosition.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
		await cts.CancelAsync().ConfigureAwait(false);

		// Assert - polling should eventually query from beginning
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", -1L, A<CancellationToken>._))
			.MustHaveHappened();
		await _sut.UnsubscribeAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task SubscribeAsyncUsesStartPositionFromOptions()
	{
		// Arrange
		_options.StartPosition = SubscriptionStartPosition.Position;
		_options.StartPositionValue = 42L;
		_options.PollingInterval = TimeSpan.FromMilliseconds(25);

		var observedConfiguredPosition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily((FakeItEasy.Core.IFakeObjectCall call) =>
			{
				var fromPosition = call.GetArgument<long>(2);
				if (fromPosition == 42L)
				{
					observedConfiguredPosition.TrySetResult();
				}

				return new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});

		using var cts = new CancellationTokenSource();

		// Act
		await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, cts.Token);
		await observedConfiguredPosition.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
		await cts.CancelAsync().ConfigureAwait(false);

		// Assert - polling should eventually use configured start position
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", 42L, A<CancellationToken>._))
			.MustHaveHappened();
		await _sut.UnsubscribeAsync(CancellationToken.None).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowsObjectDisposedAfterDispose()
	{
		// Arrange
		await _sut.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, CancellationToken.None));
	}

	[Fact]
	public async Task DisposeAsyncIsIdempotent()
	{
		// Act - calling DisposeAsync multiple times should not throw
		await _sut.DisposeAsync();
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task UnsubscribeAsyncHandlesNoSubscription()
	{
		// Act - unsubscribe without subscribing should not throw
		await _sut.UnsubscribeAsync(CancellationToken.None);
	}
}

#pragma warning restore CA2012
