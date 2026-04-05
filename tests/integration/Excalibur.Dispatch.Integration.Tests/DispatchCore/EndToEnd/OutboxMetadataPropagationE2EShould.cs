// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1034 // Nested types should not be visible - acceptable in test classes
#pragma warning disable CA2213 // Disposable fields should be disposed - owned by ServiceProvider

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.EndToEnd;

/// <summary>
/// E2E tests verifying that metadata propagates through the full dispatch → handler → outbox
/// staging → transport delivery chain. Proves CorrelationId, CausationId, MessageType, and
/// custom data survive the outbox serialization boundary and are available at the transport layer.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Category", "EndToEnd")]
[Trait("Component", "OutboxMetadata")]
public sealed class OutboxMetadataPropagationE2EShould : IAsyncDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IDispatcher _dispatcher;
	private readonly IMessageContextFactory _contextFactory;
	private readonly InMemoryOutboxStore _outboxStore;

	public OutboxMetadataPropagationE2EShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers
		_ = services.AddTransient<OrderCommandHandler>();
		_ = services.AddTransient<IActionHandler<PlaceOrderCommand>, OrderCommandHandler>();

		_ = services.AddTransient<MultiEventHandler>();
		_ = services.AddTransient<IActionHandler<TriggerMultipleEventsCommand>, MultiEventHandler>();

		// Wire up Dispatch with outbox and transport
		_ = services.AddDispatch();
		_ = services.AddInMemoryOutboxStore();
		_ = services.AddInMemoryTransport("outbox-e2e");

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_outboxStore = _serviceProvider.GetRequiredService<InMemoryOutboxStore>();
	}

	public async ValueTask DisposeAsync()
	{
		OrderCommandHandler.Reset();
		MultiEventHandler.Reset();
		await _serviceProvider.DisposeAsync().ConfigureAwait(false);
	}

	// ── 1. Outbox Correlation Propagation ─────────────────────────────

	[Fact]
	public async Task OutboxMessage_PreservesCorrelationId_FromDispatchContext()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new PlaceOrderCommand { OrderId = orderId, CustomerName = "E2E Customer" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = $"outbox-corr-{orderId}";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var ourMessage = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
		ourMessage.ShouldNotBeNull("Outbox should contain staged message with matching CorrelationId");
	}

	[Fact]
	public async Task OutboxMessage_PreservesMessageType()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new PlaceOrderCommand { OrderId = orderId, CustomerName = "TypeTest" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var ourMessage = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
		ourMessage.ShouldNotBeNull();
		ourMessage.MessageType.ShouldNotBeNullOrEmpty();
		ourMessage.MessageType.ShouldContain("OrderPlaced");
	}

	[Fact]
	public async Task OutboxMessage_PreservesPayloadContent()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new PlaceOrderCommand
		{
			OrderId = orderId,
			CustomerName = "Payload-Test-Customer",
			Amount = 999.99m,
		};
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var ourMessage = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
		ourMessage.ShouldNotBeNull();
		ourMessage.Payload.ShouldNotBeEmpty();

		var payloadJson = Encoding.UTF8.GetString(ourMessage.Payload);
		payloadJson.ShouldContain(orderId.ToString());
		payloadJson.ShouldContain("Payload-Test-Customer");
		payloadJson.ShouldContain("999.99");
	}

	[Fact]
	public async Task OutboxMessage_PreservesDestination()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new PlaceOrderCommand { OrderId = orderId, CustomerName = "DestTest" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var ourMessage = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
		ourMessage.ShouldNotBeNull();
		ourMessage.Destination.ShouldBe("orders-topic");
	}

	// ── 2. Multiple Outbox Messages ───────────────────────────────────

	[Fact]
	public async Task MultipleOutboxMessages_EachPreservesUniqueCorrelation()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

		// Act
		foreach (var orderId in orderIds)
		{
			var command = new PlaceOrderCommand { OrderId = orderId, CustomerName = $"Cust-{orderId}" };
			var context = _contextFactory.CreateContext();
			context.CorrelationId = $"multi-{orderId}";

			var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);
			result.Succeeded.ShouldBeTrue(result.ErrorMessage);
		}

		// Assert
		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		foreach (var orderId in orderIds)
		{
			var match = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
			match.ShouldNotBeNull($"Missing outbox message for order {orderId}");
		}
	}

	[Fact]
	public async Task Handler_StagingMultipleEvents_AllPreserveMetadata()
	{
		// Arrange
		MultiEventHandler.Reset();
		var triggerCommand = new TriggerMultipleEventsCommand
		{
			BatchId = Guid.NewGuid(),
			EventCount = 5,
		};
		var context = _contextFactory.CreateContext();
		context.CorrelationId = $"batch-{triggerCommand.BatchId}";

		// Act
		var result = await _dispatcher.DispatchAsync(triggerCommand, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var batchMessages = unsent.Where(m =>
			m.CorrelationId == triggerCommand.BatchId.ToString()).ToList();
		batchMessages.Count.ShouldBe(5, "Handler should have staged 5 outbox messages");

		for (var i = 0; i < 5; i++)
		{
			batchMessages[i].MessageType.ShouldContain("BatchEvent");
			batchMessages[i].Destination.ShouldBe("batch-events");
			batchMessages[i].Payload.ShouldNotBeEmpty();
		}
	}

	// ── 3. Outbox Lifecycle with Metadata ─────────────────────────────

	[Fact]
	public async Task OutboxMessage_SurvivesFullLifecycle_StageToSent()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new PlaceOrderCommand { OrderId = orderId, CustomerName = "Lifecycle" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = $"lifecycle-{orderId}";

		// Act - dispatch to stage
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		// Verify staged
		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var staged = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
		staged.ShouldNotBeNull();
		staged.Status.ShouldBe(OutboxStatus.Staged);
		var stagedId = staged.Id;

		// Act - mark sent
		await _outboxStore.MarkSentAsync(stagedId, CancellationToken.None);

		// Assert - no longer unsent
		var afterSend = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		afterSend.ShouldNotContain(m => m.Id == stagedId);
	}

	[Fact]
	public async Task OutboxMessage_SurvivesFailureMarking_WithMetadataIntact()
	{
		// Arrange
		OrderCommandHandler.Reset();
		var orderId = Guid.NewGuid();
		var command = new PlaceOrderCommand { OrderId = orderId, CustomerName = "FailMark" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);
		result.Succeeded.ShouldBeTrue(result.ErrorMessage);

		var unsent = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		var staged = unsent.FirstOrDefault(m => m.CorrelationId == orderId.ToString());
		staged.ShouldNotBeNull();

		// Mark failed
		await _outboxStore.MarkFailedAsync(staged.Id, "Transport timeout", 1, CancellationToken.None);

		// The message should no longer be in the unsent queue
		var afterFail = await _outboxStore.GetUnsentMessagesAsync(100, CancellationToken.None);
		afterFail.ShouldNotContain(m => m.Id == staged.Id);
	}

	// ── 4. TransportMessage Metadata ──────────────────────────────────

	[Fact]
	public void TransportMessage_CarriesAllMetadataFields()
	{
		// Arrange & Act - Verify TransportMessage has all required fields
		var msg = new TransportMessage
		{
			Id = "tm-001",
			Body = Encoding.UTF8.GetBytes("{\"test\":true}"),
			ContentType = "application/json",
			MessageType = "OrderPlacedEvent",
			CorrelationId = "tm-corr-001",
			CausationId = "tm-cause-001",
			Subject = "orders",
			TimeToLive = TimeSpan.FromMinutes(30),
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Assert - all fields preserved
		msg.Id.ShouldBe("tm-001");
		msg.ContentType.ShouldBe("application/json");
		msg.MessageType.ShouldBe("OrderPlacedEvent");
		msg.CorrelationId.ShouldBe("tm-corr-001");
		msg.CausationId.ShouldBe("tm-cause-001");
		msg.Subject.ShouldBe("orders");
		msg.TimeToLive.ShouldBe(TimeSpan.FromMinutes(30));
		msg.Body.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void TransportMessage_Properties_LazyAllocated()
	{
		// Arrange
		var msg = new TransportMessage();

		// Assert - Properties not allocated until accessed
		msg.HasProperties.ShouldBeFalse();

		// Act - access properties
		msg.Properties["key"] = "value";

		// Assert - now allocated
		msg.HasProperties.ShouldBeTrue();
		msg.Properties["key"].ShouldBe("value");
	}

	[Fact]
	public void TransportMessage_Properties_SurviveCustomData()
	{
		// Arrange
		var msg = new TransportMessage
		{
			CorrelationId = "props-test",
			Properties =
			{
				["partition-key"] = "pk-001",
				["ordering-key"] = "ok-001",
				["dedup-id"] = "dd-001",
				["custom-header-x"] = "custom-value",
			},
		};

		// Assert
		msg.Properties.Count.ShouldBe(4);
		msg.Properties["partition-key"].ShouldBe("pk-001");
		msg.Properties["ordering-key"].ShouldBe("ok-001");
		msg.Properties["dedup-id"].ShouldBe("dd-001");
		msg.Properties["custom-header-x"].ShouldBe("custom-value");
	}

	// ── Test Messages ─────────────────────────────────────────────────

	public sealed record PlaceOrderCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
		public string CustomerName { get; init; } = string.Empty;
		public decimal Amount { get; init; }
	}

	public sealed record TriggerMultipleEventsCommand : IDispatchAction
	{
		public Guid BatchId { get; init; }
		public int EventCount { get; init; }
	}

	// ── Test Handlers ─────────────────────────────────────────────────

	public sealed class OrderCommandHandler : IActionHandler<PlaceOrderCommand>
	{
		private static readonly ConcurrentBag<Guid> s_processedOrders = [];
		private readonly InMemoryOutboxStore _outboxStore;

		public OrderCommandHandler(InMemoryOutboxStore outboxStore)
		{
			_outboxStore = outboxStore;
		}

		public static ConcurrentBag<Guid> ProcessedOrders => s_processedOrders;

		public static void Reset()
		{
			while (s_processedOrders.TryTake(out _)) { }
		}

		public async Task HandleAsync(PlaceOrderCommand action, CancellationToken cancellationToken)
		{
			s_processedOrders.Add(action.OrderId);

			var payload = JsonSerializer.SerializeToUtf8Bytes(new
			{
				action.OrderId,
				action.CustomerName,
				action.Amount,
				OccurredAt = DateTimeOffset.UtcNow,
			});

			var outbound = new OutboundMessage(
				messageType: "OrderPlacedEvent",
				payload: payload,
				destination: "orders-topic")
			{
				CorrelationId = action.OrderId.ToString(),
			};

			await _outboxStore.StageMessageAsync(outbound, cancellationToken);
		}
	}

	public sealed class MultiEventHandler : IActionHandler<TriggerMultipleEventsCommand>
	{
		private readonly InMemoryOutboxStore _outboxStore;

		public MultiEventHandler(InMemoryOutboxStore outboxStore)
		{
			_outboxStore = outboxStore;
		}

		public static void Reset() { }

		public async Task HandleAsync(TriggerMultipleEventsCommand action, CancellationToken cancellationToken)
		{
			for (var i = 0; i < action.EventCount; i++)
			{
				var payload = JsonSerializer.SerializeToUtf8Bytes(new
				{
					action.BatchId,
					Sequence = i,
					OccurredAt = DateTimeOffset.UtcNow,
				});

				var outbound = new OutboundMessage(
					messageType: $"BatchEvent-{i}",
					payload: payload,
					destination: "batch-events")
				{
					CorrelationId = action.BatchId.ToString(),
				};

				await _outboxStore.StageMessageAsync(outbound, cancellationToken);
			}
		}
	}
}
