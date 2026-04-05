// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1034 // Nested types should not be visible - acceptable in test classes
#pragma warning disable CA2213 // Disposable fields should be disposed - owned by ServiceProvider

using System.Collections.Concurrent;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.EndToEnd;

/// <summary>
/// End-to-end consumer journey tests proving the core product assembles and works:
/// AddDispatch -> handler registration -> command dispatch -> outbox staging -> transport delivery.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Category", "EndToEnd")]
[Trait("Component", "ConsumerJourney")]
public sealed class ConsumerJourneyE2EShould : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IDispatcher _dispatcher;
	private readonly IMessageContextFactory _contextFactory;
	private readonly InMemoryOutboxStore _outboxStore;
	private readonly InMemoryTransportAdapter _transport;

	public ConsumerJourneyE2EShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register test handlers BEFORE AddDispatch
		_ = services.AddTransient<CreateOrderHandler>();
		_ = services.AddTransient<IActionHandler<CreateOrderCommand>, CreateOrderHandler>();

		_ = services.AddTransient<FailingHandler>();
		_ = services.AddTransient<IActionHandler<FailingCommand>, FailingHandler>();

		// Wire up Dispatch with outbox middleware
		_ = services.AddDispatch();

		// Add InMemory outbox store
		_ = services.AddInMemoryOutboxStore();

		// Add InMemory transport
		_ = services.AddInMemoryTransport("e2e-test");

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_outboxStore = _serviceProvider.GetRequiredService<InMemoryOutboxStore>();
		_transport = _serviceProvider.GetRequiredService<InMemoryTransportAdapter>();
	}

	public async ValueTask DisposeAsync()
	{
		await _serviceProvider.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public void AssembleFrameworkWithAllComponents()
	{
		// Verify all core services resolve without error
		_dispatcher.ShouldNotBeNull();
		_contextFactory.ShouldNotBeNull();
		_outboxStore.ShouldNotBeNull();
		_transport.ShouldNotBeNull();

		// Verify handler registry has our handlers
		var registry = _serviceProvider.GetRequiredService<Dispatch.Delivery.Handlers.IHandlerRegistry>();
		registry.TryGetHandler(typeof(CreateOrderCommand), out _).ShouldBeTrue(
			"CreateOrderCommand handler should be registered");
	}

	[Fact]
	public async Task DispatchCommand_HandlerIsInvoked()
	{
		// Arrange
		CreateOrderHandler.Reset();
		var command = new CreateOrderCommand
		{
			OrderId = Guid.NewGuid(),
			CustomerId = "cust-123",
			Amount = 99.95m,
		};
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue($"Dispatch failed: {result.ErrorMessage}");
		CreateOrderHandler.InvokedOrderIds.ShouldContain(command.OrderId);
		CreateOrderHandler.InvocationCount.ShouldBe(1);
	}

	[Fact]
	public async Task DispatchCommand_HandlerCanStageOutboundEvent()
	{
		// Arrange - Handler stages an outbound message in the outbox
		CreateOrderHandler.Reset();
		var command = new CreateOrderCommand
		{
			OrderId = Guid.NewGuid(),
			CustomerId = "cust-456",
			Amount = 250.00m,
		};
		var context = _contextFactory.CreateContext();

		// Act - Dispatch the command (handler will stage an outbound event)
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert - Handler ran successfully
		result.Succeeded.ShouldBeTrue($"Dispatch failed: {result.ErrorMessage}");
		CreateOrderHandler.InvokedOrderIds.ShouldContain(command.OrderId);

		// Verify outbox has the staged message
		var unsent = await _outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);
		var unsentList = unsent.ToList();
		unsentList.ShouldNotBeEmpty("Outbox should contain staged messages from the handler");

		// Find our specific message
		var ourMessage = unsentList.FirstOrDefault(m =>
			m.CorrelationId == command.OrderId.ToString());
		ourMessage.ShouldNotBeNull("Should find outbound message correlated to our command");
		ourMessage.MessageType.ShouldContain("OrderCreated");
		ourMessage.Destination.ShouldBe("orders");
		ourMessage.Status.ShouldBe(OutboxStatus.Staged);
	}

	[Fact]
	public async Task OutboxMessages_CanBeRetrievedAndMarkedSent()
	{
		// Arrange - Stage a message directly
		var outbound = new OutboundMessage(
			"TestEvent",
			Encoding.UTF8.GetBytes("{\"id\":\"test-1\"}"),
			"test-destination");

		await _outboxStore.StageMessageAsync(outbound, CancellationToken.None);

		// Act - Retrieve unsent messages
		var unsent = await _outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);
		var unsentList = unsent.ToList();

		// Assert retrieval
		unsentList.ShouldContain(m => m.Id == outbound.Id);
		var retrieved = unsentList.First(m => m.Id == outbound.Id);
		retrieved.Status.ShouldBe(OutboxStatus.Staged);
		retrieved.MessageType.ShouldBe("TestEvent");
		retrieved.Destination.ShouldBe("test-destination");

		// Act - Mark as sent
		await _outboxStore.MarkSentAsync(outbound.Id, CancellationToken.None);

		// Assert - No longer in unsent
		var afterMark = await _outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);
		afterMark.ShouldNotContain(m => m.Id == outbound.Id);
	}

	[Fact]
	public async Task OutboxMessages_CanBeMarkedFailed()
	{
		// Arrange
		var outbound = new OutboundMessage(
			"FailableEvent",
			Encoding.UTF8.GetBytes("{\"id\":\"fail-1\"}"),
			"dead-letter");

		await _outboxStore.StageMessageAsync(outbound, CancellationToken.None);

		// Act - Mark as failed
		await _outboxStore.MarkFailedAsync(outbound.Id, "Transport unavailable", 1, CancellationToken.None);

		// Assert - Not in unsent anymore (status changed)
		var unsent = await _outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None);
		unsent.ShouldNotContain(m => m.Id == outbound.Id);
	}

	[Fact]
	public async Task InMemoryTransport_IsRegisteredAndRunnable()
	{
		// Verify the transport is registered and configured
		_transport.ShouldNotBeNull();
		_transport.Name.ShouldBe("e2e-test");
		_transport.TransportType.ShouldBe("inmemory");
	}

	[Fact]
	public async Task DispatchFailingCommand_ReturnsFailureResult()
	{
		// Arrange
		var command = new FailingCommand { Reason = "Intentional test failure" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task DispatchMultipleCommands_AllHandlersInvoked()
	{
		// Arrange
		CreateOrderHandler.Reset();
		var commands = Enumerable.Range(0, 10)
			.Select(i => new CreateOrderCommand
			{
				OrderId = Guid.NewGuid(),
				CustomerId = $"cust-{i}",
				Amount = 10.0m * (i + 1),
			})
			.ToList();

		// Act
		var tasks = commands.Select(cmd =>
		{
			var context = _contextFactory.CreateContext();
			return _dispatcher.DispatchAsync(cmd, context, CancellationToken.None);
		});
		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(10);
		results.ShouldAllBe(r => r.Succeeded);
		CreateOrderHandler.InvocationCount.ShouldBe(10);

		foreach (var cmd in commands)
		{
			CreateOrderHandler.InvokedOrderIds.ShouldContain(cmd.OrderId);
		}
	}

	[Fact]
	public async Task FullJourney_DispatchToOutboxStaging()
	{
		// This is the full consumer journey test:
		// 1. Configure DI (done in constructor)
		// 2. Dispatch a command
		// 3. Verify handler executed
		// 4. Verify outbox staged the outbound event
		// 5. Verify outbox message contents are correct

		// Arrange
		CreateOrderHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new CreateOrderCommand
		{
			OrderId = orderId,
			CustomerId = "journey-customer",
			Amount = 500.00m,
		};
		var context = _contextFactory.CreateContext();
		context.CorrelationId = $"journey-{orderId}";

		// Step 1: Dispatch command
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Step 2: Verify handler executed
		result.Succeeded.ShouldBeTrue($"Dispatch failed: {result.ErrorMessage}");
		CreateOrderHandler.InvokedOrderIds.ShouldContain(orderId);

		// Step 3: Verify outbox has staged message
		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var staged = unsent.Where(m => m.CorrelationId == orderId.ToString()).ToList();
		staged.ShouldNotBeEmpty("Handler should have staged an outbound event in the outbox");

		var outboundEvent = staged[0];
		outboundEvent.MessageType.ShouldContain("OrderCreated");
		outboundEvent.Destination.ShouldBe("orders");
		outboundEvent.Payload.ShouldNotBeEmpty();
		outboundEvent.Status.ShouldBe(OutboxStatus.Staged);

		// Step 4: Verify payload is valid JSON containing expected data
		var payloadJson = Encoding.UTF8.GetString(outboundEvent.Payload);
		payloadJson.ShouldContain(orderId.ToString());
		payloadJson.ShouldContain("journey-customer");

		// Step 5: Mark as sent to complete the lifecycle
		await _outboxStore.MarkSentAsync(outboundEvent.Id, CancellationToken.None);
		var afterSend = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		afterSend.ShouldNotContain(m => m.Id == outboundEvent.Id);
	}

	[Fact]
	public async Task ContextPropagation_CorrelationIdFlowsThroughPipeline()
	{
		// Arrange
		CreateOrderHandler.Reset();
		var command = new CreateOrderCommand
		{
			OrderId = Guid.NewGuid(),
			CustomerId = "ctx-test",
			Amount = 1.00m,
		};
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "e2e-correlation-test";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		CreateOrderHandler.LastCorrelationId.ShouldBe("e2e-correlation-test");
	}

	// ── Test Messages ──────────────────────────────────────────────────

	public sealed record CreateOrderCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
		public string CustomerId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	public sealed record FailingCommand : IDispatchAction
	{
		public string Reason { get; init; } = string.Empty;
	}

	// ── Test Handlers ──────────────────────────────────────────────────

	/// <summary>
	/// Test handler that tracks invocations and stages an outbound event in the outbox.
	/// </summary>
	public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand>
	{
		private static readonly ConcurrentBag<Guid> s_invokedOrderIds = [];
		private static int s_invocationCount;
		private static string? s_lastCorrelationId;

		private readonly InMemoryOutboxStore _outboxStore;

		public CreateOrderHandler(InMemoryOutboxStore outboxStore)
		{
			_outboxStore = outboxStore;
		}

		public static ConcurrentBag<Guid> InvokedOrderIds => s_invokedOrderIds;
		public static int InvocationCount => s_invocationCount;
		public static string? LastCorrelationId => s_lastCorrelationId;

		public static void Reset()
		{
			while (s_invokedOrderIds.TryTake(out _)) { }
			Interlocked.Exchange(ref s_invocationCount, 0);
			s_lastCorrelationId = null;
		}

		public async Task HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
		{
			// Track invocation
			s_invokedOrderIds.Add(action.OrderId);
			_ = Interlocked.Increment(ref s_invocationCount);

			// Capture correlation ID from ambient context
			var ctx = MessageContextHolder.Current;
			if (ctx != null)
			{
				s_lastCorrelationId = ctx.CorrelationId;
			}

			// Stage an outbound integration event in the outbox
			var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
			{
				action.OrderId,
				action.CustomerId,
				action.Amount,
				OccurredAt = DateTimeOffset.UtcNow,
			});

			var outbound = new OutboundMessage(
				messageType: "OrderCreatedEvent",
				payload: payload,
				destination: "orders")
			{
				CorrelationId = action.OrderId.ToString(),
			};

			await _outboxStore.StageMessageAsync(outbound, cancellationToken);
		}
	}

	public sealed class FailingHandler : IActionHandler<FailingCommand>
	{
		public Task HandleAsync(FailingCommand action, CancellationToken cancellationToken)
		{
			throw new InvalidOperationException(action.Reason);
		}
	}
}
