// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.CdcAntiCorruption;

/// <summary>
/// CDC Anti-Corruption Layer - Pipeline Integration workflow tests.
/// Tests the integration of CDC events with the dispatch pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 182 - Functional Testing Epic Phase 2.
/// bd-18awn: CDC Pipeline Integration Tests (5 tests).
/// </para>
/// <para>
/// These tests use in-memory simulation to validate CDC pipeline integration patterns
/// without requiring TestContainers or external services.
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "182")]
[Trait("Component", "CdcAntiCorruption")]
[Trait("Category", "Unit")]
public sealed class CdcPipelineIntegrationWorkflowShould
{
	/// <summary>
	/// Tests that a CDC domain event flows through the full dispatch pipeline.
	/// CDC Event > Handler > Domain Event > Dispatch Pipeline > Event Handlers.
	/// </summary>
	[Fact]
	public async Task PublishToDispatchPipeline()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var dispatchPipeline = new SimulatedDispatchPipeline(executionLog);
		var cdcPipeline = new CdcToDispatchPipeline(executionLog, dispatchPipeline);

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-001",
			["total_amount"] = 250.00m,
		});

		// Act
		await cdcPipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - Event flowed through dispatch pipeline
		executionLog.Steps.ShouldContain("CdcPipeline:Start:Orders");
		executionLog.Steps.ShouldContain("CdcHandler:Orders:Insert");
		executionLog.Steps.ShouldContain("DispatchPipeline:Publish:OrderCreatedDomainEvent");
		executionLog.Steps.ShouldContain(s => s.StartsWith("EventHandler:OrderCreatedDomainEvent:Execute"));
		executionLog.Steps.ShouldContain("CdcPipeline:End:Orders");

		// Verify ordering
		var steps = executionLog.GetOrderedSteps();
		var cdcStartIndex = steps.FindIndex(s => s.Contains("CdcPipeline:Start"));
		var dispatchIndex = steps.FindIndex(s => s.Contains("DispatchPipeline:Publish"));
		var handlerIndex = steps.FindIndex(s => s.Contains("EventHandler:"));
		var cdcEndIndex = steps.FindIndex(s => s.Contains("CdcPipeline:End"));

		cdcStartIndex.ShouldBeLessThan(dispatchIndex);
		dispatchIndex.ShouldBeLessThan(handlerIndex);
		handlerIndex.ShouldBeLessThan(cdcEndIndex);
	}

	/// <summary>
	/// Tests that CDC filter rules prevent unwanted changes from propagating.
	/// Apply filter rule > Only "Orders" table changes propagate.
	/// </summary>
	[Fact]
	public async Task FilterUnwantedChanges()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var dispatchPipeline = new SimulatedDispatchPipeline(executionLog);
		var cdcPipeline = new CdcToDispatchPipeline(executionLog, dispatchPipeline);

		// Only allow Orders table events through
		cdcPipeline.SetAllowedTables(["Orders"]);

		var ordersEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-001",
		});

		var logsEvent = cdcSource.EmitInsert("AuditLogs", new Dictionary<string, object?>
		{
			["log_id"] = "LOG-001",
		});

		var usersEvent = cdcSource.EmitInsert("Users", new Dictionary<string, object?>
		{
			["user_id"] = "USR-001",
		});

		// Act
		await cdcPipeline.ProcessCdcEventAsync(ordersEvent, CancellationToken.None).ConfigureAwait(true);
		await cdcPipeline.ProcessCdcEventAsync(logsEvent, CancellationToken.None).ConfigureAwait(true);
		await cdcPipeline.ProcessCdcEventAsync(usersEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - Only Orders event was processed through the pipeline
		var publishSteps = executionLog.Steps.Where(s => s.Contains("DispatchPipeline:Publish")).ToList();
		publishSteps.Count.ShouldBe(1);
		publishSteps[0].ShouldContain("OrderCreatedDomainEvent");

		// Assert - Filtered events were logged but not processed
		executionLog.Steps.ShouldContain("CdcPipeline:Filtered:AuditLogs");
		executionLog.Steps.ShouldContain("CdcPipeline:Filtered:Users");
	}

	/// <summary>
	/// Tests that events maintain their ordering through the pipeline.
	/// Events 1,2,3 > Handlers receive in order 1,2,3.
	/// </summary>
	[Fact]
	public async Task PreserveOrderingThroughPipeline()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var dispatchPipeline = new SimulatedDispatchPipeline(executionLog);
		var cdcPipeline = new CdcToDispatchPipeline(executionLog, dispatchPipeline);

		// Emit 3 events in sequence
		var events = new[]
		{
			cdcSource.EmitInsert("Orders", new Dictionary<string, object?> { ["order_id"] = "ORD-001" }),
			cdcSource.EmitInsert("Orders", new Dictionary<string, object?> { ["order_id"] = "ORD-002" }),
			cdcSource.EmitInsert("Orders", new Dictionary<string, object?> { ["order_id"] = "ORD-003" }),
		};

		// Act - Process sequentially (as CDC would)
		foreach (var evt in events)
		{
			await cdcPipeline.ProcessCdcEventAsync(evt, CancellationToken.None).ConfigureAwait(true);
		}

		// Assert - Events were processed in order
		var handledEvents = executionLog.GetOrderedSteps()
			.Where(s => s.StartsWith("EventHandler:OrderCreatedDomainEvent:Execute:"))
			.ToList();

		handledEvents.Count.ShouldBe(3);
		handledEvents[0].ShouldContain("ORD-001");
		handledEvents[1].ShouldContain("ORD-002");
		handledEvents[2].ShouldContain("ORD-003");
	}

	/// <summary>
	/// Tests that CDC events trigger registered event handlers.
	/// OrderCreatedDomainEvent > IEventHandler&lt;OrderCreatedDomainEvent&gt; invoked.
	/// </summary>
	[Fact]
	public async Task TriggerEventHandlers()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var dispatchPipeline = new SimulatedDispatchPipeline(executionLog);
		var cdcPipeline = new CdcToDispatchPipeline(executionLog, dispatchPipeline);

		// Register multiple handlers for the same event type
		dispatchPipeline.RegisterHandler<OrderCreatedDomainEvent>("OrderNotificationHandler");
		dispatchPipeline.RegisterHandler<OrderCreatedDomainEvent>("OrderAnalyticsHandler");
		dispatchPipeline.RegisterHandler<OrderCreatedDomainEvent>("InventoryReservationHandler");

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-005",
			["total_amount"] = 500.00m,
		});

		// Act
		await cdcPipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - All registered handlers were invoked
		executionLog.Steps.ShouldContain("Handler:OrderNotificationHandler:Execute:OrderCreatedDomainEvent");
		executionLog.Steps.ShouldContain("Handler:OrderAnalyticsHandler:Execute:OrderCreatedDomainEvent");
		executionLog.Steps.ShouldContain("Handler:InventoryReservationHandler:Execute:OrderCreatedDomainEvent");
	}

	/// <summary>
	/// Tests that CDC events trigger projection updates (read model sync).
	/// Domain event > projection ApplyAsync called with correct event.
	/// </summary>
	[Fact]
	public async Task TriggerProjections()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var dispatchPipeline = new SimulatedDispatchPipeline(executionLog);
		var cdcPipeline = new CdcToDispatchPipeline(executionLog, dispatchPipeline);

		// Register projections
		var orderSummaryProjection = new SimulatedProjection("OrderSummaryProjection", executionLog);
		var salesReportProjection = new SimulatedProjection("SalesReportProjection", executionLog);
		dispatchPipeline.RegisterProjection(orderSummaryProjection);
		dispatchPipeline.RegisterProjection(salesReportProjection);

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-010",
			["total_amount"] = 1000.00m,
		});

		// Act
		await cdcPipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - All projections received the event
		executionLog.Steps.ShouldContain("Projection:OrderSummaryProjection:Apply:OrderCreatedDomainEvent");
		executionLog.Steps.ShouldContain("Projection:SalesReportProjection:Apply:OrderCreatedDomainEvent");

		// Verify projection state was updated
		orderSummaryProjection.AppliedEvents.Count.ShouldBe(1);
		_ = orderSummaryProjection.AppliedEvents[0].ShouldBeOfType<OrderCreatedDomainEvent>();

		var appliedOrder = (OrderCreatedDomainEvent)orderSummaryProjection.AppliedEvents[0];
		appliedOrder.OrderId.ShouldBe("ORD-010");
		appliedOrder.TotalAmount.ShouldBe(1000.00m);
	}

	#region Test Infrastructure

	internal enum CdcChangeType
	{
		Insert,
		Update,
		Delete,
	}

	internal interface IDomainEvent
	{
	}

	/// <summary>
	/// Execution log to track CDC and dispatch pipeline steps.
	/// </summary>
	internal sealed class ExecutionLog
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
	/// Simulated CDC source that emits controlled events.
	/// </summary>
	internal sealed class SimulatedCdcSource
	{
		private int _sequenceNumber;

		public SimulatedCdcEvent EmitInsert(string tableName, Dictionary<string, object?> columns)
		{
			var lsn = BitConverter.GetBytes(++_sequenceNumber);
			return new SimulatedCdcEvent
			{
				Lsn = lsn,
				TableName = tableName,
				ChangeType = CdcChangeType.Insert,
				Columns = columns,
			};
		}
	}

	/// <summary>
	/// CDC to Dispatch pipeline integration.
	/// </summary>
	internal sealed class CdcToDispatchPipeline
	{
		private readonly ExecutionLog _log;
		private readonly SimulatedDispatchPipeline _dispatchPipeline;
		private HashSet<string>? _allowedTables;

		public CdcToDispatchPipeline(ExecutionLog log, SimulatedDispatchPipeline dispatchPipeline)
		{
			_log = log;
			_dispatchPipeline = dispatchPipeline;
		}

		public void SetAllowedTables(string[] tables) => _allowedTables = [.. tables];

		public async Task ProcessCdcEventAsync(SimulatedCdcEvent cdcEvent, CancellationToken cancellationToken)
		{
			_log.Log($"CdcPipeline:Start:{cdcEvent.TableName}");

			// Apply table filter
			if (_allowedTables != null && !_allowedTables.Contains(cdcEvent.TableName))
			{
				_log.Log($"CdcPipeline:Filtered:{cdcEvent.TableName}");
				_log.Log($"CdcPipeline:End:{cdcEvent.TableName}");
				return;
			}

			// Transform CDC event to domain event
			_log.Log($"CdcHandler:{cdcEvent.TableName}:{cdcEvent.ChangeType}");
			var domainEvent = TransformToDomainEvent(cdcEvent);

			// Publish through dispatch pipeline
			await _dispatchPipeline.PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);

			_log.Log($"CdcPipeline:End:{cdcEvent.TableName}");
		}

		private static IDomainEvent TransformToDomainEvent(SimulatedCdcEvent cdcEvent)
		{
			return cdcEvent.TableName switch
			{
				"Orders" => new OrderCreatedDomainEvent
				{
					OrderId = cdcEvent.Columns.GetValueOrDefault("order_id")?.ToString() ?? string.Empty,
					TotalAmount = cdcEvent.Columns.TryGetValue("total_amount", out var amount) && amount is decimal d
						? d
						: 0m,
				},
				_ => new GenericDomainEvent
				{
					TableName = cdcEvent.TableName,
					Data = cdcEvent.Columns,
				},
			};
		}
	}

	/// <summary>
	/// Simulated dispatch pipeline.
	/// </summary>
	internal sealed class SimulatedDispatchPipeline
	{
		private readonly ExecutionLog _log;
		private readonly Dictionary<Type, List<string>> _handlers = [];
		private readonly List<SimulatedProjection> _projections = [];

		public SimulatedDispatchPipeline(ExecutionLog log)
		{
			_log = log;
			// Default handler
			RegisterHandler<OrderCreatedDomainEvent>("DefaultOrderHandler");
		}

		public void RegisterHandler<T>(string handlerName) where T : IDomainEvent
		{
			var eventType = typeof(T);
			if (!_handlers.ContainsKey(eventType))
			{
				_handlers[eventType] = [];
			}

			_handlers[eventType].Add(handlerName);
		}

		public void RegisterProjection(SimulatedProjection projection)
		{
			_projections.Add(projection);
		}

		public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
		{
			var eventTypeName = domainEvent.GetType().Name;
			_log.Log($"DispatchPipeline:Publish:{eventTypeName}");

			// Invoke handlers
			var eventType = domainEvent.GetType();
			if (_handlers.TryGetValue(eventType, out var handlers))
			{
				foreach (var handler in handlers)
				{
					_log.Log($"Handler:{handler}:Execute:{eventTypeName}");
				}
			}

			// Default handler for new events
			_log.Log($"EventHandler:{eventTypeName}:Execute:{GetEventId(domainEvent)}");

			// Apply to projections
			foreach (var projection in _projections)
			{
				await projection.ApplyAsync(domainEvent, cancellationToken).ConfigureAwait(false);
			}
		}

		private static string GetEventId(IDomainEvent domainEvent)
		{
			return domainEvent switch
			{
				OrderCreatedDomainEvent order => order.OrderId,
				_ => "Unknown",
			};
		}
	}

	/// <summary>
	/// Simulated projection.
	/// </summary>
	internal sealed class SimulatedProjection
	{
		private readonly string _name;
		private readonly ExecutionLog _log;

		public SimulatedProjection(string name, ExecutionLog log)
		{
			_name = name;
			_log = log;
		}

		public List<IDomainEvent> AppliedEvents { get; } = [];

		public Task ApplyAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
		{
			var eventTypeName = domainEvent.GetType().Name;
			_log.Log($"Projection:{_name}:Apply:{eventTypeName}");
			AppliedEvents.Add(domainEvent);
			return Task.CompletedTask;
		}
	}

	// CDC types
	internal sealed class SimulatedCdcEvent
	{
		public byte[] Lsn { get; init; } = [];
		public string TableName { get; init; } = string.Empty;
		public CdcChangeType ChangeType { get; init; }
		public Dictionary<string, object?> Columns { get; init; } = [];
	}

	// Domain event types
	internal sealed class OrderCreatedDomainEvent : IDomainEvent
	{
		public string OrderId { get; init; } = string.Empty;
		public decimal TotalAmount { get; init; }
	}

	internal sealed class GenericDomainEvent : IDomainEvent
	{
		public string TableName { get; init; } = string.Empty;
		public Dictionary<string, object?> Data { get; init; } = [];
	}

	#endregion Test Infrastructure
}
