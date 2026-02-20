// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.EventSourcing.Abstractions;

namespace Tests.Shared.Conformance.Integration;

/// <summary>
/// Base class for integration tests verifying saga orchestrating event-sourced aggregates.
/// Tests the interaction between ISagaStore and IEventStore in coordinated workflows.
/// </summary>
/// <remarks>
/// <para>
/// This integration test kit verifies that sagas can correctly orchestrate
/// event-sourced aggregates, including:
/// </para>
/// <list type="bullet">
///   <item>Saga creates events that are appended to an event store</item>
///   <item>Events loaded from the store can drive saga state transitions</item>
///   <item>Saga completion correlates with aggregate state</item>
///   <item>Error handling across saga and event store boundaries</item>
///   <item>Concurrent saga operations with event sourced aggregates</item>
/// </list>
/// <para>
/// To create integration tests for your implementation:
/// <list type="number">
///   <item>Inherit from SagaEventSourcingIntegrationTestBase</item>
///   <item>Override CreateEventStoreAsync() and CreateSagaStoreAsync()</item>
///   <item>Override CleanupAsync() for resource cleanup</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "SagaEventSourcing")]
public abstract class SagaEventSourcingIntegrationTestBase : IAsyncLifetime
{
	/// <summary>
	/// The event store instance for aggregate event persistence.
	/// </summary>
	protected IEventStore EventStore { get; private set; } = null!;

	/// <summary>
	/// The saga store instance for saga state persistence.
	/// </summary>
	protected ISagaStore SagaStore { get; private set; } = null!;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		EventStore = await CreateEventStoreAsync().ConfigureAwait(false);
		SagaStore = await CreateSagaStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		if (EventStore is IAsyncDisposable eventStoreDisposable)
		{
			await eventStoreDisposable.DisposeAsync().ConfigureAwait(false);
		}

		if (SagaStore is IAsyncDisposable sagaStoreDisposable)
		{
			await sagaStoreDisposable.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a new instance of the IEventStore implementation.
	/// </summary>
	protected abstract Task<IEventStore> CreateEventStoreAsync();

	/// <summary>
	/// Creates a new instance of the ISagaStore implementation.
	/// </summary>
	protected abstract Task<ISagaStore> CreateSagaStoreAsync();

	/// <summary>
	/// Cleans up resources after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	#region Helper Types

	/// <summary>
	/// Test aggregate type name.
	/// </summary>
	protected const string TestAggregateType = "OrderAggregate";

	/// <summary>
	/// Creates a test domain event.
	/// </summary>
	protected static IntegrationTestDomainEvent CreateTestEvent(
		string aggregateId,
		string eventType = "OrderPlaced",
		string? data = null)
	{
		return new IntegrationTestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = eventType,
			Data = data ?? $"TestData-{Guid.NewGuid():N}"
		};
	}

	#endregion Helper Types

	#region Saga Creates Events Tests

	[Fact]
	public async Task SagaCreatesEvents_EventsPersistedToStore()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var aggregateId = Guid.NewGuid().ToString();
		var sagaState = new OrderSagaState
		{
			SagaId = sagaId,
			OrderId = aggregateId,
			Status = "Created"
		};

		// Act - Saga produces events and saves state
		var events = new[]
		{
			CreateTestEvent(aggregateId, "OrderCreated", "Order initiated by saga"),
			CreateTestEvent(aggregateId, "PaymentRequested", "Payment requested by saga")
		};

		var appendResult = await EventStore.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		sagaState.Status = "PaymentPending";
		await SagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Assert
		appendResult.Success.ShouldBeTrue("Events should be appended to event store");

		var loadedEvents = await EventStore.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		loadedEvents.Count.ShouldBe(2);

		var loadedSaga = await SagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		loadedSaga.ShouldNotBeNull();
		loadedSaga.Status.ShouldBe("PaymentPending");
	}

	#endregion Saga Creates Events Tests

	#region Events Drive Saga Tests

	[Fact]
	public async Task EventsDriveSaga_SagaStateUpdatedByEvents()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var aggregateId = Guid.NewGuid().ToString();

		// Save initial saga state
		var sagaState = new OrderSagaState
		{
			SagaId = sagaId,
			OrderId = aggregateId,
			Status = "Created"
		};
		await SagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Append event to the aggregate
		var events = new[] { CreateTestEvent(aggregateId, "PaymentConfirmed") };
		await EventStore.AppendAsync(
			aggregateId,
			TestAggregateType,
			events,
			expectedVersion: -1,
			CancellationToken.None).ConfigureAwait(false);

		// Act - Load events and update saga based on them
		var loadedEvents = await EventStore.LoadAsync(
			aggregateId,
			TestAggregateType,
			CancellationToken.None).ConfigureAwait(false);

		var loadedSaga = await SagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		loadedSaga.ShouldNotBeNull();

		// Simulate saga processing the event
		loadedSaga.Status = "PaymentConfirmed";
		loadedSaga.EventsProcessed = loadedEvents.Count;
		await SagaStore.SaveAsync(loadedSaga, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var finalSaga = await SagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		finalSaga.ShouldNotBeNull();
		finalSaga.Status.ShouldBe("PaymentConfirmed");
		finalSaga.EventsProcessed.ShouldBe(1);
	}

	#endregion Events Drive Saga Tests

	#region Saga Completion Tests

	[Fact]
	public async Task SagaCompletion_CorrelatesWithAggregateState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var aggregateId = Guid.NewGuid().ToString();

		var sagaState = new OrderSagaState
		{
			SagaId = sagaId,
			OrderId = aggregateId,
			Status = "Created"
		};

		// Simulate full saga lifecycle
		var createdEvents = new[] { CreateTestEvent(aggregateId, "OrderCreated") };
		await EventStore.AppendAsync(
			aggregateId, TestAggregateType, createdEvents,
			expectedVersion: -1, CancellationToken.None).ConfigureAwait(false);
		await SagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		var confirmedEvents = new[] { CreateTestEvent(aggregateId, "OrderConfirmed") };
		await EventStore.AppendAsync(
			aggregateId, TestAggregateType, confirmedEvents,
			expectedVersion: 0, CancellationToken.None).ConfigureAwait(false);

		// Act - Complete the saga
		sagaState.Status = "Completed";
		sagaState.Completed = true;
		await SagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var finalSaga = await SagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		finalSaga.ShouldNotBeNull();
		finalSaga.Completed.ShouldBeTrue();

		var allEvents = await EventStore.LoadAsync(
			aggregateId, TestAggregateType, CancellationToken.None).ConfigureAwait(false);
		allEvents.Count.ShouldBe(2, "All aggregate events should be preserved");
	}

	#endregion Saga Completion Tests

	#region Error Handling Tests

	[Fact]
	public async Task EventStoreConcurrencyConflict_SagaCanRetry()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var aggregateId = Guid.NewGuid().ToString();

		var initialEvents = new[] { CreateTestEvent(aggregateId, "OrderCreated") };
		await EventStore.AppendAsync(
			aggregateId, TestAggregateType, initialEvents,
			expectedVersion: -1, CancellationToken.None).ConfigureAwait(false);

		var sagaState = new OrderSagaState
		{
			SagaId = sagaId,
			OrderId = aggregateId,
			Status = "Created"
		};
		await SagaStore.SaveAsync(sagaState, CancellationToken.None).ConfigureAwait(false);

		// Act - Simulate concurrency conflict (wrong version)
		var conflictEvents = new[] { CreateTestEvent(aggregateId, "ShippingStarted") };
		var conflictResult = await EventStore.AppendAsync(
			aggregateId, TestAggregateType, conflictEvents,
			expectedVersion: 99, // Wrong version
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		conflictResult.Success.ShouldBeFalse("Should detect concurrency conflict");
		conflictResult.IsConcurrencyConflict.ShouldBeTrue();

		// Retry with correct version
		var retryResult = await EventStore.AppendAsync(
			aggregateId, TestAggregateType, conflictEvents,
			expectedVersion: 0, // Correct version
			CancellationToken.None).ConfigureAwait(false);

		retryResult.Success.ShouldBeTrue("Retry with correct version should succeed");
	}

	#endregion Error Handling Tests

	#region Concurrent Operations Tests

	[Fact]
	public async Task ConcurrentSagaAndEventOperations_MaintainConsistency()
	{
		// Arrange
		const int sagaCount = 5;
		var sagas = new List<(Guid SagaId, string AggregateId)>();

		for (int i = 0; i < sagaCount; i++)
		{
			sagas.Add((Guid.NewGuid(), Guid.NewGuid().ToString()));
		}

		// Act - Run concurrent saga+event operations
		var tasks = sagas.Select(async s =>
		{
			var state = new OrderSagaState
			{
				SagaId = s.SagaId,
				OrderId = s.AggregateId,
				Status = "Processing"
			};

			var events = new[] { CreateTestEvent(s.AggregateId, "OrderCreated") };

			await EventStore.AppendAsync(
				s.AggregateId, TestAggregateType, events,
				expectedVersion: -1, CancellationToken.None).ConfigureAwait(false);

			await SagaStore.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All sagas and events should be independently accessible
		foreach (var (sagaId, aggregateId) in sagas)
		{
			var saga = await SagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
				.ConfigureAwait(false);
			saga.ShouldNotBeNull($"Saga {sagaId} should exist");

			var events = await EventStore.LoadAsync(
				aggregateId, TestAggregateType, CancellationToken.None)
				.ConfigureAwait(false);
			events.Count.ShouldBe(1, $"Aggregate {aggregateId} should have 1 event");
		}
	}

	#endregion Concurrent Operations Tests
}

/// <summary>
/// Test saga state for order workflow integration testing.
/// </summary>
public class OrderSagaState : SagaState
{
	/// <summary>
	/// Gets or sets the order aggregate ID correlated with this saga.
	/// </summary>
	public string OrderId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the current saga status.
	/// </summary>
	public string Status { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the count of events processed by this saga.
	/// </summary>
	public int EventsProcessed { get; set; }
}

/// <summary>
/// Test domain event for integration testing.
/// </summary>
public class IntegrationTestDomainEvent : IDomainEvent
{
	/// <inheritdoc/>
	public string EventId { get; set; } = Guid.NewGuid().ToString();

	/// <inheritdoc/>
	public string AggregateId { get; set; } = string.Empty;

	/// <inheritdoc/>
	public long Version { get; set; }

	/// <inheritdoc/>
	public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

	/// <inheritdoc/>
	public string EventType { get; set; } = string.Empty;

	/// <inheritdoc/>
	public IDictionary<string, object>? Metadata { get; set; }

	/// <summary>
	/// Gets or sets test data for the event.
	/// </summary>
	public string Data { get; set; } = string.Empty;
}
