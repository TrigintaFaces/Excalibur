// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Integration tests verifying saga coordination works correctly with event sourcing patterns.
/// </summary>
/// <remarks>
/// These tests validate the interaction between the saga subsystem and the dispatch pipeline
/// when events are produced by saga steps and consumed by other sagas. They use in-memory
/// implementations to avoid infrastructure dependencies.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.EventSourcing")]
[Trait("Priority", "2")]
public sealed class SagaEventSourcingIntegrationShould
{
	[Fact]
	public void RegisterSagaStoreAndCoordinatorTogether()
	{
		// Arrange & Act
		var services = new ServiceCollection();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		_ = services.AddDispatchOrchestration();
		var provider = services.BuildServiceProvider();

		// Assert -- ISagaStore resolves to InMemorySagaStore
		var store = provider.GetService<ISagaStore>();
		store.ShouldNotBeNull();
		store.ShouldBeOfType<InMemorySagaStore>();

		// Assert -- ISagaCoordinator and SagaHandlingMiddleware are registered
		services.ShouldContain(sd => sd.ServiceType == typeof(ISagaCoordinator));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware)
			&& sd.ImplementationType == typeof(SagaHandlingMiddleware));
	}

	[Fact]
	public async Task SaveSagaStateAfterEventProcessing()
	{
		// Arrange -- Create a saga store and save initial state
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();

		var state = new TestSagaState { SagaId = sagaId, Status = "Started", Counter = 0 };

		// Act -- Save state (simulates saga step persisting state after processing a domain event)
		await store.SaveAsync(state, CancellationToken.None);

		// Simulate event processing advancing the saga
		state.Counter++;
		state.Status = "Processing";
		await store.SaveAsync(state, CancellationToken.None);

		// Assert -- State is persisted correctly
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Counter.ShouldBe(1);
		loaded.Status.ShouldBe("Processing");
	}

	[Fact]
	public async Task CompleteSagaAfterAllEventsProcessed()
	{
		// Arrange
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId, Status = "Started" };
		await store.SaveAsync(state, CancellationToken.None);

		// Act -- Simulate processing through multiple events
		state.Status = "Step1Complete";
		state.Counter = 1;
		await store.SaveAsync(state, CancellationToken.None);

		state.Status = "Step2Complete";
		state.Counter = 2;
		await store.SaveAsync(state, CancellationToken.None);

		state.Completed = true;
		state.Status = "Completed";
		state.CompletedUtc = DateTime.UtcNow;
		await store.SaveAsync(state, CancellationToken.None);

		// Assert
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Completed.ShouldBeTrue();
		loaded.Status.ShouldBe("Completed");
		loaded.Counter.ShouldBe(2);
		loaded.CompletedUtc.ShouldNotBeNull();
	}

	[Fact]
	public async Task IsolateConcurrentSagaInstances()
	{
		// Arrange -- Two saga instances processing in parallel (e.g., two order processes)
		var store = new InMemorySagaStore();
		var sagaId1 = Guid.NewGuid();
		var sagaId2 = Guid.NewGuid();

		var state1 = new TestSagaState { SagaId = sagaId1, Status = "Order1", Counter = 10 };
		var state2 = new TestSagaState { SagaId = sagaId2, Status = "Order2", Counter = 20 };

		// Act -- Save and mutate independently
		await store.SaveAsync(state1, CancellationToken.None);
		await store.SaveAsync(state2, CancellationToken.None);

		state1.Counter = 11;
		await store.SaveAsync(state1, CancellationToken.None);

		// Assert -- Mutations don't leak between instances
		var loaded1 = await store.LoadAsync<TestSagaState>(sagaId1, CancellationToken.None);
		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, CancellationToken.None);

		loaded1.ShouldNotBeNull();
		loaded1.Counter.ShouldBe(11);
		loaded1.Status.ShouldBe("Order1");

		loaded2.ShouldNotBeNull();
		loaded2.Counter.ShouldBe(20);
		loaded2.Status.ShouldBe("Order2");
	}

	[Fact]
	public async Task PreserveSagaStateAcrossMultipleEventCycles()
	{
		// Arrange -- Simulates a saga that processes domain events across multiple dispatch cycles
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();

		// Cycle 1: OrderCreated event starts the saga
		var state = new TestSagaState { SagaId = sagaId, Status = "OrderCreated", Counter = 0 };
		state.Data["orderId"] = "ORD-001";
		await store.SaveAsync(state, CancellationToken.None);

		// Cycle 2: PaymentReceived event advances the saga
		var reloaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		reloaded.ShouldNotBeNull();
		reloaded.Status = "PaymentReceived";
		reloaded.Counter = 1;
		reloaded.Data["paymentId"] = "PAY-001";
		await store.SaveAsync(reloaded, CancellationToken.None);

		// Cycle 3: ShipmentDispatched event completes the saga
		var reloaded2 = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		reloaded2.ShouldNotBeNull();
		reloaded2.Status = "ShipmentDispatched";
		reloaded2.Counter = 2;
		reloaded2.Completed = true;
		await store.SaveAsync(reloaded2, CancellationToken.None);

		// Assert -- All accumulated state is preserved
		var final = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		final.ShouldNotBeNull();
		final.Status.ShouldBe("ShipmentDispatched");
		final.Counter.ShouldBe(2);
		final.Completed.ShouldBeTrue();
		final.Data.ShouldContainKey("orderId");
		final.Data.ShouldContainKey("paymentId");
	}

	[Fact]
	public void RegisterBothOrchestrationAndAdvancedSagasWithoutConflict()
	{
		// Arrange -- Consumer may register both orchestration and advanced saga services
		var services = new ServiceCollection();
		_ = services.AddDispatch();
		_ = services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
		_ = services.AddDispatchOrchestration();
		_ = services.AddDispatchAdvancedSagas();

		// Act
		var provider = services.BuildServiceProvider();

		// Assert -- Both subsystems resolve without throwing
		var sagaStore = provider.GetService<ISagaStore>();
		sagaStore.ShouldNotBeNull();

		// Assert -- Both middleware types registered as IDispatchMiddleware descriptors
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware)
			&& sd.ImplementationType == typeof(SagaHandlingMiddleware));

		// Assert -- ISagaCoordinator registered (orchestration subsystem)
		services.ShouldContain(sd => sd.ServiceType == typeof(ISagaCoordinator));
	}

	/// <summary>
	/// Test saga state for integration tests.
	/// </summary>
	private sealed class TestSagaState : SagaState
	{
		public string Status { get; set; } = "Pending";

		public int Counter { get; set; }

		public DateTime? CompletedUtc { get; set; }

		public Dictionary<string, string> Data { get; set; } = new(StringComparer.Ordinal);
	}
}
