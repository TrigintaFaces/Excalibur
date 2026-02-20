// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.CdcMessageTranslation;

/// <summary>
/// CDC Anti-Corruption Layer - Message Translation functional tests.
/// Tests the translation of CDC events into commands through the anti-corruption layer.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 196 - Functional Testing Epic Phase 2.
/// bd-9rkno: CDC Message Translation Tests (5 tests).
/// </para>
/// <para>
/// These tests validate the CDC → Command → Dispatch pattern where CDC events
/// are translated to commands (not direct domain events) to protect the domain model
/// from external data structure changes.
/// </para>
/// <para>
/// Anti-Corruption Layer Pattern:
/// CDC Event → Translator → Command → Dispatch → Handler → Domain Event
/// </para>
/// </remarks>
[FunctionalTest]
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "196")]
[Trait("Component", "CdcAntiCorruption")]
public sealed class CdcMessageTranslationWorkflowTests : FunctionalTestBase
{
	/// <summary>
	/// Tests that a CDC insert event is translated to a CreateCommand and dispatched.
	/// CDC Insert → CreateOrderCommand → OrderCreatedDomainEvent.
	/// </summary>
	[Fact]
	public async Task CDC_Translator_Converts_Insert_To_DomainEvent()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var translator = new CdcToCommandTranslator(executionLog);
		var dispatcher = new SimulatedDispatcher(executionLog);
		var pipeline = new CdcAntiCorruptionPipeline(executionLog, translator, dispatcher);

		var cdcEvent = CreateCdcInsertEvent("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-001",
			["customer_id"] = "CUST-123",
			["total_amount"] = 150.00m,
			["created_at"] = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
		});

		// Act
		await pipeline.ProcessAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - CDC event was translated to command
		translator.TranslatedCommands.Count.ShouldBe(1);
		var command = translator.TranslatedCommands[0];
		_ = command.ShouldBeOfType<CreateOrderCommand>();

		var createOrder = (CreateOrderCommand)command;
		createOrder.OrderId.ShouldBe("ORD-001");
		createOrder.CustomerId.ShouldBe("CUST-123");
		createOrder.TotalAmount.ShouldBe(150.00m);

		// Assert - Command was dispatched and handler produced domain event
		dispatcher.DispatchedCommands.Count.ShouldBe(1);
		dispatcher.ProducedEvents.Count.ShouldBe(1);
		_ = dispatcher.ProducedEvents[0].ShouldBeOfType<OrderCreatedDomainEvent>();

		// Assert - Execution order is correct
		var steps = executionLog.GetOrderedSteps();
		var translateIndex = steps.FindIndex(s => s.Contains("Translator:Insert"));
		var dispatchIndex = steps.FindIndex(s => s.Contains("Dispatcher:Command"));
		var handlerIndex = steps.FindIndex(s => s.Contains("Handler:Execute"));

		translateIndex.ShouldBeLessThan(dispatchIndex);
		dispatchIndex.ShouldBeLessThan(handlerIndex);
	}

	/// <summary>
	/// Tests that a CDC update event is translated to an UpdateCommand and dispatched.
	/// CDC Update → UpdateOrderCommand → OrderUpdatedDomainEvent.
	/// </summary>
	[Fact]
	public async Task CDC_Translator_Converts_Update_To_DomainEvent()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var translator = new CdcToCommandTranslator(executionLog);
		var dispatcher = new SimulatedDispatcher(executionLog);
		var pipeline = new CdcAntiCorruptionPipeline(executionLog, translator, dispatcher);

		var cdcEvent = CreateCdcUpdateEvent("Orders",
			before: new Dictionary<string, object?>
			{
				["order_id"] = "ORD-002",
				["total_amount"] = 100.00m,
				["status"] = "Pending",
			},
			after: new Dictionary<string, object?>
			{
				["order_id"] = "ORD-002",
				["total_amount"] = 120.00m,
				["status"] = "Confirmed",
			});

		// Act
		await pipeline.ProcessAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - CDC event was translated to update command
		translator.TranslatedCommands.Count.ShouldBe(1);
		var command = translator.TranslatedCommands[0];
		_ = command.ShouldBeOfType<UpdateOrderCommand>();

		var updateOrder = (UpdateOrderCommand)command;
		updateOrder.OrderId.ShouldBe("ORD-002");
		updateOrder.NewTotalAmount.ShouldBe(120.00m);
		updateOrder.NewStatus.ShouldBe("Confirmed");
		updateOrder.OldTotalAmount.ShouldBe(100.00m);
		updateOrder.OldStatus.ShouldBe("Pending");

		// Assert - Handler produced update domain event
		dispatcher.ProducedEvents.Count.ShouldBe(1);
		_ = dispatcher.ProducedEvents[0].ShouldBeOfType<OrderUpdatedDomainEvent>();
	}

	/// <summary>
	/// Tests that a CDC delete event is translated to a DeleteCommand and dispatched.
	/// CDC Delete → DeleteOrderCommand → OrderDeletedDomainEvent.
	/// </summary>
	[Fact]
	public async Task CDC_Translator_Converts_Delete_To_DomainEvent()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var translator = new CdcToCommandTranslator(executionLog);
		var dispatcher = new SimulatedDispatcher(executionLog);
		var pipeline = new CdcAntiCorruptionPipeline(executionLog, translator, dispatcher);

		var cdcEvent = CreateCdcDeleteEvent("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-003",
			["customer_id"] = "CUST-456",
			["total_amount"] = 200.00m,
		});

		// Act
		await pipeline.ProcessAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - CDC event was translated to delete command
		translator.TranslatedCommands.Count.ShouldBe(1);
		var command = translator.TranslatedCommands[0];
		_ = command.ShouldBeOfType<DeleteOrderCommand>();

		var deleteOrder = (DeleteOrderCommand)command;
		deleteOrder.OrderId.ShouldBe("ORD-003");
		deleteOrder.Reason.ShouldBe("CDC Delete");

		// Assert - Handler produced delete domain event
		dispatcher.ProducedEvents.Count.ShouldBe(1);
		_ = dispatcher.ProducedEvents[0].ShouldBeOfType<OrderDeletedDomainEvent>();
	}

	/// <summary>
	/// Tests that batch CDC changes are translated and dispatched in order.
	/// Multiple CDC events → Multiple commands → Multiple domain events (ordered).
	/// </summary>
	[Fact]
	public async Task CDC_Translator_Handles_Batch_Changes()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var translator = new CdcToCommandTranslator(executionLog);
		var dispatcher = new SimulatedDispatcher(executionLog);
		var pipeline = new CdcAntiCorruptionPipeline(executionLog, translator, dispatcher);

		var cdcEvents = new[]
		{
			CreateCdcInsertEvent("Orders", new Dictionary<string, object?> { ["order_id"] = "BATCH-001", ["total_amount"] = 10.00m }),
			CreateCdcInsertEvent("Orders", new Dictionary<string, object?> { ["order_id"] = "BATCH-002", ["total_amount"] = 20.00m }),
			CreateCdcInsertEvent("Orders", new Dictionary<string, object?> { ["order_id"] = "BATCH-003", ["total_amount"] = 30.00m }),
			CreateCdcUpdateEvent("Orders",
				before: new Dictionary<string, object?> { ["order_id"] = "BATCH-001", ["total_amount"] = 10.00m },
				after: new Dictionary<string, object?> { ["order_id"] = "BATCH-001", ["total_amount"] = 15.00m }),
			CreateCdcDeleteEvent("Orders", new Dictionary<string, object?> { ["order_id"] = "BATCH-003" }),
		};

		// Act - Process batch in order
		foreach (var cdcEvent in cdcEvents)
		{
			await pipeline.ProcessAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);
		}

		// Assert - All events were translated and dispatched
		translator.TranslatedCommands.Count.ShouldBe(5);
		dispatcher.DispatchedCommands.Count.ShouldBe(5);
		dispatcher.ProducedEvents.Count.ShouldBe(5);

		// Assert - Order was preserved
		_ = dispatcher.ProducedEvents[0].ShouldBeOfType<OrderCreatedDomainEvent>();
		_ = dispatcher.ProducedEvents[1].ShouldBeOfType<OrderCreatedDomainEvent>();
		_ = dispatcher.ProducedEvents[2].ShouldBeOfType<OrderCreatedDomainEvent>();
		_ = dispatcher.ProducedEvents[3].ShouldBeOfType<OrderUpdatedDomainEvent>();
		_ = dispatcher.ProducedEvents[4].ShouldBeOfType<OrderDeletedDomainEvent>();

		// Assert - Correct order IDs
		((OrderCreatedDomainEvent)dispatcher.ProducedEvents[0]).OrderId.ShouldBe("BATCH-001");
		((OrderCreatedDomainEvent)dispatcher.ProducedEvents[1]).OrderId.ShouldBe("BATCH-002");
		((OrderCreatedDomainEvent)dispatcher.ProducedEvents[2]).OrderId.ShouldBe("BATCH-003");
	}

	/// <summary>
	/// Tests that the translator validates data integrity before producing commands.
	/// Invalid CDC data → Validation error → No command dispatched.
	/// </summary>
	[Fact]
	public async Task CDC_Translator_Validates_Data_Integrity()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var translator = new CdcToCommandTranslator(executionLog);
		var dispatcher = new SimulatedDispatcher(executionLog);
		var pipeline = new CdcAntiCorruptionPipeline(executionLog, translator, dispatcher);

		// Create invalid CDC events for validation testing
		var invalidEvents = new[]
		{
			// Missing required field (order_id is null)
			CreateCdcInsertEvent("Orders", new Dictionary<string, object?>
			{
				["order_id"] = null,
				["total_amount"] = 100.00m,
			}),
			// Invalid amount (negative)
			CreateCdcInsertEvent("Orders", new Dictionary<string, object?>
			{
				["order_id"] = "INVALID-002",
				["total_amount"] = -50.00m,
			}),
		};

		var validEvent = CreateCdcInsertEvent("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "VALID-001",
			["total_amount"] = 75.00m,
		});

		// Act - Process invalid events (should be filtered/logged)
		foreach (var invalidEvent in invalidEvents)
		{
			await pipeline.ProcessAsync(invalidEvent, CancellationToken.None).ConfigureAwait(true);
		}

		// Process valid event
		await pipeline.ProcessAsync(validEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - Only valid event was dispatched
		dispatcher.DispatchedCommands.Count.ShouldBe(1);
		var command = dispatcher.DispatchedCommands[0] as CreateOrderCommand;
		_ = command.ShouldNotBeNull();
		command.OrderId.ShouldBe("VALID-001");

		// Assert - Invalid events were logged as validation failures
		executionLog.Steps.ShouldContain(s => s.Contains("Validation:Failed"));
		var validationFailures = executionLog.Steps.Count(s => s.Contains("Validation:Failed"));
		validationFailures.ShouldBe(2);
	}

	#region Test Infrastructure

	private enum CdcChangeType
	{
		Insert,
		Update,
		Delete,
	}

	/// <summary>
	/// Creates a simulated CDC insert event.
	/// </summary>
	private static SimulatedCdcEvent CreateCdcInsertEvent(string tableName, Dictionary<string, object?> columns)
	{
		return new SimulatedCdcEvent
		{
			Lsn = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
			TableName = tableName,
			ChangeType = CdcChangeType.Insert,
			NewValues = columns,
			OldValues = new Dictionary<string, object?>(),
		};
	}

	/// <summary>
	/// Creates a simulated CDC update event.
	/// </summary>
	private static SimulatedCdcEvent CreateCdcUpdateEvent(
		string tableName,
		Dictionary<string, object?> before,
		Dictionary<string, object?> after)
	{
		return new SimulatedCdcEvent
		{
			Lsn = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
			TableName = tableName,
			ChangeType = CdcChangeType.Update,
			OldValues = before,
			NewValues = after,
		};
	}

	/// <summary>
	/// Creates a simulated CDC delete event.
	/// </summary>
	private static SimulatedCdcEvent CreateCdcDeleteEvent(string tableName, Dictionary<string, object?> columns)
	{
		return new SimulatedCdcEvent
		{
			Lsn = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
			TableName = tableName,
			ChangeType = CdcChangeType.Delete,
			OldValues = columns,
			NewValues = new Dictionary<string, object?>(),
		};
	}

	/// <summary>
	/// Execution log to track CDC anti-corruption layer steps.
	/// </summary>
	private sealed class ExecutionLog
	{
		private readonly ConcurrentQueue<string> _orderedSteps = new();
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step)
		{
			Steps.Add(step);
			_orderedSteps.Enqueue(step);
		}

		public List<string> GetOrderedSteps() => [.. _orderedSteps];
	}

	/// <summary>
	/// Anti-corruption layer pipeline: CDC → Command → Excalibur.Dispatch.
	/// </summary>
	private sealed class CdcAntiCorruptionPipeline
	{
		private readonly ExecutionLog _log;
		private readonly CdcToCommandTranslator _translator;
		private readonly SimulatedDispatcher _dispatcher;

		public CdcAntiCorruptionPipeline(
			ExecutionLog log,
			CdcToCommandTranslator translator,
			SimulatedDispatcher dispatcher)
		{
			_log = log;
			_translator = translator;
			_dispatcher = dispatcher;
		}

		public async Task ProcessAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken)
		{
			_log.Log($"Pipeline:Start:{cdcEvent.TableName}:{cdcEvent.ChangeType}");

			// Step 1: Translate CDC event to command (anti-corruption layer)
			var command = _translator.Translate(cdcEvent);

			if (command == null)
			{
				_log.Log($"Pipeline:Skipped:{cdcEvent.TableName}:NoCommand");
				return;
			}

			// Step 2: Dispatch command through the domain
			await _dispatcher.DispatchAsync(command, cancellationToken).ConfigureAwait(false);

			_log.Log($"Pipeline:End:{cdcEvent.TableName}:{cdcEvent.ChangeType}");
		}
	}

	/// <summary>
	/// Translator that converts CDC events to commands (anti-corruption layer).
	/// </summary>
	private sealed class CdcToCommandTranslator
	{
		private readonly ExecutionLog _log;

		public CdcToCommandTranslator(ExecutionLog log)
		{
			_log = log;
		}

		public List<object> TranslatedCommands { get; } = [];

		public object? Translate(SimulatedCdcEvent cdcEvent)
		{
			_log.Log($"Translator:{cdcEvent.ChangeType}:{cdcEvent.TableName}");

			// Validate data integrity
			if (!ValidateEvent(cdcEvent))
			{
				return null;
			}

			var command = cdcEvent.TableName switch
			{
				"Orders" => TranslateOrderEvent(cdcEvent),
				_ => null,
			};

			if (command != null)
			{
				TranslatedCommands.Add(command);
			}

			return command;
		}

		private bool ValidateEvent(SimulatedCdcEvent cdcEvent)
		{
			var values = cdcEvent.ChangeType == CdcChangeType.Delete
				? cdcEvent.OldValues
				: cdcEvent.NewValues;

			// Check for null primary key
			if (values.TryGetValue("order_id", out var orderId) && orderId == null)
			{
				_log.Log($"Validation:Failed:order_id:Null");
				return false;
			}

			// Check for negative amounts
			if (values.TryGetValue("total_amount", out var amount) && amount is decimal d && d < 0)
			{
				_log.Log($"Validation:Failed:total_amount:Negative");
				return false;
			}

			_log.Log($"Validation:Passed:{cdcEvent.TableName}");
			return true;
		}

		private object? TranslateOrderEvent(SimulatedCdcEvent cdcEvent)
		{
			return cdcEvent.ChangeType switch
			{
				CdcChangeType.Insert => new CreateOrderCommand
				{
					OrderId = cdcEvent.NewValues.GetValueOrDefault("order_id")?.ToString() ?? string.Empty,
					CustomerId = cdcEvent.NewValues.GetValueOrDefault("customer_id")?.ToString() ?? string.Empty,
					TotalAmount = cdcEvent.NewValues.TryGetValue("total_amount", out var a) && a is decimal d ? d : 0m,
				},
				CdcChangeType.Update => new UpdateOrderCommand
				{
					OrderId = cdcEvent.NewValues.GetValueOrDefault("order_id")?.ToString() ?? string.Empty,
					OldTotalAmount = cdcEvent.OldValues.TryGetValue("total_amount", out var oa) && oa is decimal od ? od : 0m,
					NewTotalAmount = cdcEvent.NewValues.TryGetValue("total_amount", out var na) && na is decimal nd ? nd : 0m,
					OldStatus = cdcEvent.OldValues.GetValueOrDefault("status")?.ToString(),
					NewStatus = cdcEvent.NewValues.GetValueOrDefault("status")?.ToString(),
				},
				CdcChangeType.Delete => new DeleteOrderCommand
				{
					OrderId = cdcEvent.OldValues.GetValueOrDefault("order_id")?.ToString() ?? string.Empty,
					Reason = "CDC Delete",
				},
				_ => null,
			};
		}
	}

	/// <summary>
	/// Simulated dispatcher that processes commands and produces domain events.
	/// </summary>
	private sealed class SimulatedDispatcher
	{
		private readonly ExecutionLog _log;

		public SimulatedDispatcher(ExecutionLog log)
		{
			_log = log;
		}

		public List<object> DispatchedCommands { get; } = [];
		public List<object> ProducedEvents { get; } = [];

		public Task DispatchAsync(object command, CancellationToken cancellationToken)
		{
			_log.Log($"Dispatcher:Command:{command.GetType().Name}");
			DispatchedCommands.Add(command);

			// Simulate handler execution
			_log.Log($"Handler:Execute:{command.GetType().Name}");

			// Produce domain event based on command type
			var domainEvent = command switch
			{
				CreateOrderCommand create => (object)new OrderCreatedDomainEvent
				{
					OrderId = create.OrderId,
					CustomerId = create.CustomerId,
					TotalAmount = create.TotalAmount,
				},
				UpdateOrderCommand update => new OrderUpdatedDomainEvent
				{
					OrderId = update.OrderId,
					OldTotalAmount = update.OldTotalAmount,
					NewTotalAmount = update.NewTotalAmount,
				},
				DeleteOrderCommand delete => new OrderDeletedDomainEvent
				{
					OrderId = delete.OrderId,
					Reason = delete.Reason,
				},
				_ => null,
			};

			if (domainEvent != null)
			{
				ProducedEvents.Add(domainEvent);
				_log.Log($"Handler:ProducedEvent:{domainEvent.GetType().Name}");
			}

			return Task.CompletedTask;
		}
	}

	// CDC Types
	private sealed class SimulatedCdcEvent
	{
		public byte[] Lsn { get; init; } = [];
		public string TableName { get; init; } = string.Empty;
		public CdcChangeType ChangeType { get; init; }
		public Dictionary<string, object?> OldValues { get; init; } = [];
		public Dictionary<string, object?> NewValues { get; init; } = [];
	}

	// Command Types (Anti-Corruption Layer output)

	private sealed class CreateOrderCommand
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
		public decimal TotalAmount { get; init; }
	}

	private sealed class UpdateOrderCommand
	{
		public string OrderId { get; init; } = string.Empty;
		public decimal OldTotalAmount { get; init; }
		public decimal NewTotalAmount { get; init; }
		public string? OldStatus { get; init; }
		public string? NewStatus { get; init; }
	}

	private sealed class DeleteOrderCommand
	{
		public string OrderId { get; init; } = string.Empty;
		public string Reason { get; init; } = string.Empty;
	}

	// Domain Event Types (Handler output)

	private sealed class OrderCreatedDomainEvent
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
		public decimal TotalAmount { get; init; }
	}

	private sealed class OrderUpdatedDomainEvent
	{
		public string OrderId { get; init; } = string.Empty;
		public decimal OldTotalAmount { get; init; }
		public decimal NewTotalAmount { get; init; }
	}

	private sealed class OrderDeletedDomainEvent
	{
		public string OrderId { get; init; } = string.Empty;
		public string Reason { get; init; } = string.Empty;
	}

	#endregion Test Infrastructure
}
