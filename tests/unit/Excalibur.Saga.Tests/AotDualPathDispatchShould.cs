// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.StateMachine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests;

/// <summary>
/// End-to-end dual-path AOT verification tests for Saga dispatch registries.
/// Validates that <c>AddSaga&lt;TSaga, TSagaState&gt;()</c> populates both
/// <see cref="ISagaDispatchRegistry"/> and <see cref="SagaContextFactoryRegistry"/>
/// so the AOT code path does not throw <see cref="PlatformNotSupportedException"/>.
/// Sprint 755 task b3awop (R-B6).
/// </summary>
[Collection("SagaStaticRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "AOT")]
public sealed class AotDualPathDispatchShould : IDisposable
{
	public AotDualPathDispatchShould()
	{
		// Clear static state before each test to prevent cross-test contamination
		SagaContextFactoryRegistry.Clear();
		SagaServiceCollectionExtensions.SagaPendingDispatchRegistrations.Clear();
	}

	public void Dispose()
	{
		SagaContextFactoryRegistry.Clear();
		SagaServiceCollectionExtensions.SagaPendingDispatchRegistrations.Clear();
	}

	// -- ISagaDispatchRegistry Population Tests --

	[Fact]
	public void PopulateDispatchRegistryViaAddSaga()
	{
		// Arrange — full DI registration
		var services = new ServiceCollection();
		services.AddExcaliburSaga();
		services.AddSaga<TestOrderSaga, TestOrderSagaState>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		// Force options resolution to trigger populator
		_ = sp.GetRequiredService<IOptions<SagaOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<ISagaDispatchRegistry>();
		var dispatcher = registry.GetDispatcher(typeof(TestOrderSaga), typeof(TestOrderSagaState));

		// Assert — registry is populated, NOT null
		dispatcher.ShouldNotBeNull("ISagaDispatchRegistry should be populated by AddSaga<TSaga, TSagaState>(). " +
			"A null dispatcher means the AOT path would throw PlatformNotSupportedException.");
	}

	[Fact]
	public void PopulateTypeRegistryViaAddSaga()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburSaga();
		services.AddSaga<TestOrderSaga, TestOrderSagaState>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<SagaOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<ISagaTypeRegistry>();
		var resolved = registry.ResolveType(typeof(TestOrderSaga).FullName!);

		// Assert
		resolved.ShouldBe(typeof(TestOrderSaga));
	}

	[Fact]
	public void ReturnNullDispatcherForUnregisteredSaga()
	{
		// Arrange — register one saga but query for another
		var services = new ServiceCollection();
		services.AddExcaliburSaga();
		services.AddSaga<TestOrderSaga, TestOrderSagaState>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<SagaOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<ISagaDispatchRegistry>();
		var dispatcher = registry.GetDispatcher(typeof(string), typeof(int));

		// Assert — unregistered types return null (not exception)
		dispatcher.ShouldBeNull();
	}

	// -- SagaContextFactoryRegistry Population Tests --

	[Fact]
	public void PopulateContextFactoryRegistryViaStateDefinition()
	{
		// Arrange — StateDefinition.When<TMessage>() should call
		// SagaContextFactoryRegistry.Register<TData, TMessage>()
		var definition = new StateDefinition<TestOrderSagaState>("Initial");
		definition.When<TestOrderCreated>(_ => { });

		// Act
		var context = SagaContextFactoryRegistry.CreateContext(
			typeof(TestOrderSagaState), typeof(TestOrderCreated),
			new TestOrderSagaState(), new TestOrderCreated(), null!);

		// Assert — factory populated, returns typed context
		context.ShouldNotBeNull("SagaContextFactoryRegistry should be populated by StateDefinition.When<TMessage>(). " +
			"A null result means the AOT path would throw PlatformNotSupportedException.");
		context.ShouldBeOfType<SagaContext<TestOrderSagaState, TestOrderCreated>>();
	}

	[Fact]
	public void ReturnNullContextForUnregisteredMessageType()
	{
		// Arrange — register one message type but query for another
		var definition = new StateDefinition<TestOrderSagaState>("Initial");
		definition.When<TestOrderCreated>(_ => { });

		// Act
		var context = SagaContextFactoryRegistry.CreateContext(
			typeof(TestOrderSagaState), typeof(TestOrderShipped),
			new TestOrderSagaState(), new TestOrderShipped(), null!);

		// Assert
		context.ShouldBeNull();
	}

	[Fact]
	public void CreateCorrectTypedContextFromFactory()
	{
		// Arrange
		var definition = new StateDefinition<TestOrderSagaState>("Initial");
		definition.When<TestOrderCreated>(_ => { });

		var state = new TestOrderSagaState { OrderId = "order-42" };
		var message = new TestOrderCreated { OrderId = "order-42", Amount = 100m };

		// Act
		var context = SagaContextFactoryRegistry.CreateContext(
			typeof(TestOrderSagaState), typeof(TestOrderCreated),
			state, message, null!);

		// Assert — context has correct typed data and message
		var typed = context.ShouldBeOfType<SagaContext<TestOrderSagaState, TestOrderCreated>>();
		typed.Data.ShouldBeSameAs(state);
		typed.Message.ShouldBeSameAs(message);
		typed.Data.OrderId.ShouldBe("order-42");
		typed.Message.Amount.ShouldBe(100m);
	}

	// -- Multiple Saga Registration Tests --

	[Fact]
	public void PopulateRegistriesForMultipleSagas()
	{
		// Arrange — register two different saga types
		var services = new ServiceCollection();
		services.AddExcaliburSaga();
		services.AddSaga<TestOrderSaga, TestOrderSagaState>();
		services.AddSaga<TestPaymentSaga, TestPaymentSagaState>();
		services.AddLogging();
		var sp = services.BuildServiceProvider();

		_ = sp.GetRequiredService<IOptions<SagaOptions>>().Value;

		// Act
		var registry = sp.GetRequiredService<ISagaDispatchRegistry>();

		// Assert — both sagas have dispatch delegates
		registry.GetDispatcher(typeof(TestOrderSaga), typeof(TestOrderSagaState)).ShouldNotBeNull();
		registry.GetDispatcher(typeof(TestPaymentSaga), typeof(TestPaymentSagaState)).ShouldNotBeNull();
	}

	[Fact]
	public void ContextFactoryRegistrationIsIdempotent()
	{
		// Arrange — register same type pair twice (idempotent)
		var definition = new StateDefinition<TestOrderSagaState>("Initial");
		definition.When<TestOrderCreated>(_ => { });
		definition.When<TestOrderCreated>(_ => { }); // duplicate — should not throw

		// Act
		var context = SagaContextFactoryRegistry.CreateContext(
			typeof(TestOrderSagaState), typeof(TestOrderCreated),
			new TestOrderSagaState(), new TestOrderCreated(), null!);

		// Assert
		context.ShouldNotBeNull();
	}

	// -- Test Fixtures --

	internal sealed class TestOrderSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}

	internal sealed class TestPaymentSagaState : SagaState
	{
		public string PaymentId { get; set; } = string.Empty;
	}

	internal sealed class TestOrderCreated : ISagaEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "order-1";
		public long Version { get; init; } = 1;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TestOrderCreated);
		public IDictionary<string, object>? Metadata { get; init; }
		public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
		public string OrderId { get; init; } = "order-1";
		public decimal Amount { get; init; } = 99.99m;
	}

	internal sealed class TestOrderShipped : ISagaEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public string AggregateId { get; init; } = "order-1";
		public long Version { get; init; } = 2;
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TestOrderShipped);
		public IDictionary<string, object>? Metadata { get; init; }
		public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
		public string SagaId { get; init; } = Guid.NewGuid().ToString();
		public string? StepId { get; init; }
	}

	internal sealed class TestOrderSaga(
		TestOrderSagaState initialState,
		IDispatcher dispatcher,
		Microsoft.Extensions.Logging.ILogger logger)
		: SagaBase<TestOrderSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is TestOrderCreated;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	internal sealed class TestPaymentSaga(
		TestPaymentSagaState initialState,
		IDispatcher dispatcher,
		Microsoft.Extensions.Logging.ILogger logger)
		: SagaBase<TestPaymentSagaState>(initialState, dispatcher, logger)
	{
		public override bool HandlesEvent(object eventMessage) => eventMessage is TestOrderCreated;

		public override Task HandleAsync(object eventMessage, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
