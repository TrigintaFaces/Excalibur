// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Subscriptions;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class EventStoreLiveSubscriptionShould : IAsyncDisposable
{
	private readonly IEventStore _eventStore;
	private readonly IEventSerializer _eventSerializer;
	private readonly EventStoreLiveSubscription _sut;

	public EventStoreLiveSubscriptionShould()
	{
		_eventStore = A.Fake<IEventStore>();
		_eventSerializer = A.Fake<IEventSerializer>();
		_sut = new EventStoreLiveSubscription(
			_eventStore,
			_eventSerializer,
			new EventSubscriptionOptions
			{
				PollingInterval = TimeSpan.FromMilliseconds(50),
				MaxBatchSize = 10,
				StartPosition = SubscriptionStartPosition.Beginning
			},
			NullLogger<EventStoreLiveSubscription>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task SubscribeAsync_StartPolling()
	{
		// Arrange
		var loadObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", A<long>._, A<CancellationToken>._))
			.Invokes(() => loadObserved.TrySetResult())
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));
#pragma warning restore CA2012

		var receivedEvents = new List<IDomainEvent>();

		// Act
		await _sut.SubscribeAsync("stream-1", events =>
		{
			receivedEvents.AddRange(events);
			return Task.CompletedTask;
		}, CancellationToken.None);

		await loadObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

		// Assert - should have attempted to load events
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", A<long>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task SubscribeAsync_DeliverEventsToHandler()
	{
		// Arrange
		var storedEvents = new List<StoredEvent>
		{
			new("e1", "agg-1", "Type", "TestEvent", Array.Empty<byte>(), null, 1, DateTimeOffset.UtcNow, false)
		};

		var domainEvent = A.Fake<IDomainEvent>();
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync("stream-1", "stream-1", A<long>._, A<CancellationToken>._))
			.ReturnsNextFromSequence(
				new ValueTask<IReadOnlyList<StoredEvent>>(storedEvents),
				new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));
#pragma warning restore CA2012

		A.CallTo(() => _eventSerializer.ResolveType("TestEvent")).Returns(typeof(IDomainEvent));
		A.CallTo(() => _eventSerializer.DeserializeEvent(A<byte[]>._, A<Type>._)).Returns(domainEvent);

		var received = new List<IDomainEvent>();
		var eventObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Act
		await _sut.SubscribeAsync("stream-1", events =>
		{
			received.AddRange(events);
			if (received.Count > 0)
			{
				eventObserved.TrySetResult();
			}
			return Task.CompletedTask;
		}, CancellationToken.None);

		await eventObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

		// Assert
		received.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task UnsubscribeAsync_StopPolling()
	{
		// Arrange
		var loadObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.Invokes(() => loadObserved.TrySetResult())
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));
#pragma warning restore CA2012

		await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, CancellationToken.None);
		await loadObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

		// Act
		await _sut.UnsubscribeAsync(CancellationToken.None);

		// Assert - should complete without error
	}

	[Fact]
	public async Task SubscribeAsync_ThrowOnNullOrEmptyStreamId()
	{
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.SubscribeAsync(null!, _ => Task.CompletedTask, CancellationToken.None));
		await Should.ThrowAsync<ArgumentException>(
			() => _sut.SubscribeAsync("", _ => Task.CompletedTask, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsync_ThrowOnNullHandler()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SubscribeAsync("stream-1", null!, CancellationToken.None));
	}

	[Fact]
	public async Task DisposeAsync_BeIdempotent()
	{
		// Act & Assert - should not throw
		await _sut.DisposeAsync();
		await _sut.DisposeAsync();
	}

	[Fact]
	public async Task SubscribeAsync_ThrowWhenDisposed()
	{
		// Arrange
		await _sut.DisposeAsync();

		// Act & Assert
		await Should.ThrowAsync<ObjectDisposedException>(
			() => _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, CancellationToken.None));
	}

	[Fact]
	public void ThrowOnNullConstructorArgs()
	{
		var es = A.Fake<IEventStore>();
		var serializer = A.Fake<IEventSerializer>();
		var options = new EventSubscriptionOptions();
		var logger = NullLogger<EventStoreLiveSubscription>.Instance;

		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(null!, serializer, options, logger));
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(es, null!, options, logger));
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(es, serializer, null!, logger));
		Should.Throw<ArgumentNullException>(() => new EventStoreLiveSubscription(es, serializer, options, null!));
	}

	[Fact]
	public async Task ContinuePolling_AfterTransientError()
	{
		// Arrange
		var callCount = 0;
		var secondCallObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				callCount++;
				if (callCount >= 2)
				{
					secondCallObserved.TrySetResult();
				}

				return callCount == 1
					? throw new InvalidOperationException("transient")
					: new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>());
			});
#pragma warning restore CA2012

		// Act
		await _sut.SubscribeAsync("stream-1", _ => Task.CompletedTask, CancellationToken.None);
		await secondCallObserved.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

		// Assert - should have retried after error
		callCount.ShouldBeGreaterThan(1);
	}

	private void SetupEmptyLoad()
	{
#pragma warning disable CA2012
		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<long>._, A<CancellationToken>._))
			.Returns(new ValueTask<IReadOnlyList<StoredEvent>>(Array.Empty<StoredEvent>()));
#pragma warning restore CA2012
	}
}
