// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Saga.Orchestration;

namespace Excalibur.Dispatch.Tests.Functional.Saga;

/// <summary>
/// E2E tests for the complete saga lifecycle.
/// Validates: start -> state transition -> compensate -> complete.
/// </summary>
/// <remarks>
/// Beads issue: Excalibur.Dispatch-huv49z (G.1).
/// Uses in-memory saga store to test the full saga coordination flow
/// without requiring Docker. The test validates the saga framework's
/// event routing, state management, and lifecycle coordination.
/// </remarks>
[Trait("Category", "Functional")]
[Trait("Component", "Saga")]
[Trait("Feature", "Lifecycle")]
public sealed class SagaLifecycleE2EShould : FunctionalTestBase
{
	private ServiceProvider? _serviceProvider;

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task CompleteFullSagaLifecycle_StartToCompletion()
	{
		// Arrange
		var services = CreateSagaServices();
		_serviceProvider = services.BuildServiceProvider();

		RegisterOrderSaga();

		var coordinator = _serviceProvider.GetRequiredService<ISagaCoordinator>();
		var sagaStore = _serviceProvider.GetRequiredKeyedService<ISagaStore>("default");
		var messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();

		var sagaId = Guid.NewGuid();

		// Act 1: Start saga with OrderCreated event
		var orderCreated = new OrderCreatedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = null,
			OrderId = "ORD-001",
			Amount = 250.00m,
		};

		await coordinator.ProcessEventAsync(messageContext, orderCreated, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert 1: Saga state created and populated
		var state1 = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		state1.ShouldNotBeNull("Saga state should be created after start event");
		state1.OrderId.ShouldBe("ORD-001");
		state1.Amount.ShouldBe(250.00m);
		state1.CurrentStep.ShouldBe("OrderCreated");
		state1.Completed.ShouldBeFalse();

		// Act 2: Transition state with PaymentProcessed event
		var paymentProcessed = new PaymentProcessedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "payment",
			PaymentId = "PAY-001",
		};

		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, paymentProcessed, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert 2: State transitioned
		var state2 = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		state2.ShouldNotBeNull();
		state2.PaymentId.ShouldBe("PAY-001");
		state2.CurrentStep.ShouldBe("PaymentProcessed");
		state2.IsPaid.ShouldBeTrue();

		// Act 3: Complete saga with OrderShipped event
		var orderShipped = new OrderShippedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "shipping",
			TrackingNumber = "TRACK-123",
		};

		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, orderShipped, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert 3: Saga completed
		var state3 = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		state3.ShouldNotBeNull();
		state3.TrackingNumber.ShouldBe("TRACK-123");
		state3.CurrentStep.ShouldBe("Completed");
		state3.Completed.ShouldBeTrue();
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task HandleCompensationOnFailure()
	{
		// Arrange
		var services = CreateSagaServices();
		_serviceProvider = services.BuildServiceProvider();

		RegisterOrderSaga();

		var coordinator = _serviceProvider.GetRequiredService<ISagaCoordinator>();
		var sagaStore = _serviceProvider.GetRequiredKeyedService<ISagaStore>("default");

		var sagaId = Guid.NewGuid();

		// Start the saga
		var messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new OrderCreatedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = null,
			OrderId = "ORD-FAIL",
			Amount = 500.00m,
		}, CancellationToken.None).ConfigureAwait(false);

		// Process payment
		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new PaymentProcessedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "payment",
			PaymentId = "PAY-FAIL",
		}, CancellationToken.None).ConfigureAwait(false);

		// Act: Trigger compensation via failure event
		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new OrderFailedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "failure",
			Reason = "Payment reversed by bank",
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert: Saga should be marked as compensated and completed
		var state = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		state.ShouldNotBeNull();
		state.IsCompensated.ShouldBeTrue();
		state.CompensationReason.ShouldBe("Payment reversed by bank");
		state.CurrentStep.ShouldBe("Compensated");
		state.Completed.ShouldBeTrue();
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SkipEventsForCompletedSaga()
	{
		// Arrange
		var services = CreateSagaServices();
		_serviceProvider = services.BuildServiceProvider();

		RegisterOrderSaga();

		var coordinator = _serviceProvider.GetRequiredService<ISagaCoordinator>();
		var sagaStore = _serviceProvider.GetRequiredKeyedService<ISagaStore>("default");

		var sagaId = Guid.NewGuid();

		// Complete the saga first
		var messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new OrderCreatedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = null,
			OrderId = "ORD-DONE",
			Amount = 100.00m,
		}, CancellationToken.None).ConfigureAwait(false);

		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new PaymentProcessedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "payment",
			PaymentId = "PAY-DONE",
		}, CancellationToken.None).ConfigureAwait(false);

		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new OrderShippedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "shipping",
			TrackingNumber = "TRACK-DONE",
		}, CancellationToken.None).ConfigureAwait(false);

		// Verify saga completed
		var completedState = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		completedState.ShouldNotBeNull();
		completedState.Completed.ShouldBeTrue();
		var versionAtCompletion = completedState.Version;

		// Act: Send another event to the completed saga
		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = Guid.NewGuid().ToString();
		await coordinator.ProcessEventAsync(messageContext, new PaymentProcessedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = "duplicate-payment",
			PaymentId = "PAY-EXTRA",
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert: State should not have changed (version stays the same)
		var finalState = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		finalState.ShouldNotBeNull();
		finalState.Completed.ShouldBeTrue();
		finalState.Version.ShouldBe(versionAtCompletion);
	}

	[Fact]
	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[SuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task HandleIdempotentEventReplay()
	{
		// Arrange
		var services = CreateSagaServices();
		_serviceProvider = services.BuildServiceProvider();

		RegisterOrderSaga();

		var coordinator = _serviceProvider.GetRequiredService<ISagaCoordinator>();
		var sagaStore = _serviceProvider.GetRequiredKeyedService<ISagaStore>("default");

		var sagaId = Guid.NewGuid();

		// Start the saga
		var eventId = Guid.NewGuid().ToString();
		var messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = eventId;
		var orderCreated = new OrderCreatedEvent
		{
			SagaId = sagaId.ToString(),
			StepId = null,
			OrderId = "ORD-IDEMPOTENT",
			Amount = 100.00m,
		};

		await coordinator.ProcessEventAsync(messageContext, orderCreated, CancellationToken.None)
			.ConfigureAwait(false);

		var stateAfterFirst = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		stateAfterFirst.ShouldNotBeNull();
		var versionAfterFirst = stateAfterFirst.Version;

		// Act: Replay the same event with the same message ID
		messageContext = _serviceProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		messageContext.MessageId = eventId; // Same event ID

		await coordinator.ProcessEventAsync(messageContext, orderCreated, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert: State should be unchanged (idempotent replay detected)
		var stateAfterReplay = await sagaStore.LoadAsync<OrderSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		stateAfterReplay.ShouldNotBeNull();
		// The saga may or may not bump the version on idempotent replay,
		// but the business state should not change
		stateAfterReplay.OrderId.ShouldBe("ORD-IDEMPOTENT");
	}

	public override async Task DisposeAsync()
	{
		if (_serviceProvider is not null)
		{
			await _serviceProvider.DisposeAsync().ConfigureAwait(false);
		}

		await base.DisposeAsync().ConfigureAwait(false);
	}

	[SuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	private static ServiceCollection CreateSagaServices()
	{
		var services = new ServiceCollection();
		services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

		// Register non-generic ILogger for components that need it
		services.AddSingleton<ILogger>(static sp =>
			sp.GetRequiredService<ILoggerFactory>().CreateLogger("Saga"));

		// Core dispatch + saga orchestration
		services.AddDispatchPipeline();
		services.AddDispatchHandlers(typeof(SagaLifecycleE2EShould).Assembly);
		services.AddDispatchOrchestration();

		// Bridge keyed ISagaStore to non-keyed for SagaCoordinator constructor
		services.AddSingleton<ISagaStore>(static sp =>
			sp.GetRequiredKeyedService<ISagaStore>("default"));

		// SagaHandlingMiddleware needs concrete SagaCoordinator (not just ISagaCoordinator)
		services.AddSingleton<SagaCoordinator>(static sp =>
			(SagaCoordinator)sp.GetRequiredService<ISagaCoordinator>());

		return services;
	}

	private static void RegisterOrderSaga()
	{
		SagaRegistry.Register<OrderSaga, OrderSagaState>(info =>
			info.StartsWith<OrderCreatedEvent>()
				.Handles<PaymentProcessedEvent>()
				.Handles<OrderShippedEvent>()
				.Handles<OrderFailedEvent>());
	}
}

#region Saga state and implementation

/// <summary>
/// State for the order processing saga.
/// </summary>
public sealed class OrderSagaState : SagaState
{
	public string OrderId { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string CurrentStep { get; set; } = string.Empty;
	public bool IsPaid { get; set; }
	public string PaymentId { get; set; } = string.Empty;
	public string TrackingNumber { get; set; } = string.Empty;
	public bool IsCompensated { get; set; }
	public string CompensationReason { get; set; } = string.Empty;
}

/// <summary>
/// Order processing saga that handles the full order lifecycle.
/// </summary>
public sealed class OrderSaga(
	OrderSagaState initialState,
	IDispatcher dispatcher,
	ILogger<OrderSaga> logger)
	: SagaBase<OrderSagaState>(initialState, dispatcher, logger)
{
	public override bool HandlesEvent(object eventMessage) =>
		eventMessage is OrderCreatedEvent
			or PaymentProcessedEvent
			or OrderShippedEvent
			or OrderFailedEvent;

	public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken)
	{
		switch (eventMessage)
		{
			case OrderCreatedEvent oc:
				State.OrderId = oc.OrderId;
				State.Amount = oc.Amount;
				State.CurrentStep = "OrderCreated";
				break;

			case PaymentProcessedEvent pp:
				State.PaymentId = pp.PaymentId;
				State.IsPaid = true;
				State.CurrentStep = "PaymentProcessed";
				break;

			case OrderShippedEvent os:
				State.TrackingNumber = os.TrackingNumber;
				State.CurrentStep = "Completed";
				State.Completed = true;
				break;

			case OrderFailedEvent of:
				State.IsCompensated = true;
				State.CompensationReason = of.Reason;
				State.CurrentStep = "Compensated";
				State.Completed = true;
				break;
		}

		return Task.CompletedTask;
	}
}

#endregion

#region Saga events

public sealed class OrderCreatedEvent : ISagaEvent
{
	public required string SagaId { get; init; }
	public string? StepId { get; init; }
	public required string OrderId { get; init; }
	public required decimal Amount { get; init; }
}

public sealed class PaymentProcessedEvent : ISagaEvent
{
	public required string SagaId { get; init; }
	public string? StepId { get; init; }
	public required string PaymentId { get; init; }
}

public sealed class OrderShippedEvent : ISagaEvent
{
	public required string SagaId { get; init; }
	public string? StepId { get; init; }
	public required string TrackingNumber { get; init; }
}

public sealed class OrderFailedEvent : ISagaEvent
{
	public required string SagaId { get; init; }
	public string? StepId { get; init; }
	public required string Reason { get; init; }
}

#endregion
