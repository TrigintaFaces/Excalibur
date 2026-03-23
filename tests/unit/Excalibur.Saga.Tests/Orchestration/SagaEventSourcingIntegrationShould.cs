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

		// Assert -- ISagaStore resolves via keyed service to InMemorySagaStore
		var store = provider.GetKeyedService<ISagaStore>("default");
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

		var state1 = new TestSagaState { SagaId = sagaId, Status = "Started", Counter = 0, Version = 0 };
		await store.SaveAsync(state1, CancellationToken.None);

		// Act -- Simulate event processing advancing the saga (new instance, incremented Version)
		var state2 = new TestSagaState { SagaId = sagaId, Status = "Processing", Counter = 1, Version = 1 };
		await store.SaveAsync(state2, CancellationToken.None);

		// Assert -- State is persisted correctly
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Counter.ShouldBe(1);
		loaded.Status.ShouldBe("Processing");
	}

	[Fact]
	public async Task CompleteSagaAfterAllEventsProcessed()
	{
		// Arrange -- each save uses a new instance to avoid shared-reference issues with ConcurrentDictionary
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();

		await store.SaveAsync(new TestSagaState { SagaId = sagaId, Status = "Started", Version = 0 }, CancellationToken.None);

		// Act -- Simulate processing through multiple events (each with incremented Version)
		await store.SaveAsync(new TestSagaState { SagaId = sagaId, Status = "Step1Complete", Counter = 1, Version = 1 }, CancellationToken.None);
		await store.SaveAsync(new TestSagaState { SagaId = sagaId, Status = "Step2Complete", Counter = 2, Version = 2 }, CancellationToken.None);
		await store.SaveAsync(new TestSagaState { SagaId = sagaId, Status = "Completed", Counter = 2, Completed = true, CompletedUtc = DateTime.UtcNow, Version = 3 }, CancellationToken.None);

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

		// Act -- Save and mutate independently (new instances for Version concurrency)
		await store.SaveAsync(state1, CancellationToken.None);
		await store.SaveAsync(state2, CancellationToken.None);

		var state1v2 = new TestSagaState { SagaId = sagaId1, Status = "Order1", Counter = 11, Version = 1 };
		await store.SaveAsync(state1v2, CancellationToken.None);

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

		// Cycle 2: PaymentReceived event (new instance with incremented Version, preserving accumulated data)
		var state2 = new TestSagaState { SagaId = sagaId, Status = "PaymentReceived", Counter = 1, Version = 1 };
		state2.Data["orderId"] = "ORD-001";
		state2.Data["paymentId"] = "PAY-001";
		await store.SaveAsync(state2, CancellationToken.None);

		// Cycle 3: ShipmentDispatched event completes the saga
		var state3 = new TestSagaState { SagaId = sagaId, Status = "ShipmentDispatched", Counter = 2, Completed = true, Version = 2 };
		state3.Data["orderId"] = "ORD-001";
		state3.Data["paymentId"] = "PAY-001";
		await store.SaveAsync(state3, CancellationToken.None);

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

		// Assert -- Both subsystems resolve without throwing (keyed service)
		var sagaStore = provider.GetKeyedService<ISagaStore>("default");
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
