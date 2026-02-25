// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.CdcAntiCorruption;

/// <summary>
/// CDC Anti-Corruption Layer - Event Transformation workflow tests.
/// Tests the transformation of CDC (Change Data Capture) events into domain events.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 182 - Functional Testing Epic Phase 2.
/// bd-xzojx: CDC Event Transformation Tests (5 tests).
/// </para>
/// <para>
/// These tests use in-memory simulation to validate CDC transformation patterns
/// without requiring TestContainers or external services.
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "182")]
[Trait("Component", "CdcAntiCorruption")]
[Trait("Category", "Unit")]
public sealed class CdcEventTransformationWorkflowShould
{
	/// <summary>
	/// Tests that a CDC insert event is correctly transformed into a domain created event.
	/// CDC Insert > DataChangeEvent > OrderCreatedDomainEvent.
	/// </summary>
	[Fact]
	public async Task TransformCdcToDomainEvent()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new SimulatedDataChangeHandler("Orders", executionLog);
		var pipeline = new CdcPipeline(executionLog, handler);

		var cdcEvent = cdcSource.EmitInsert("Orders", new Dictionary<string, object?>
		{
			["order_id"] = "ORD-001",
			["total_amount"] = 150.00m,
			["customer_id"] = "CUST-123",
			["created_at"] = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc),
		});

		// Act
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - CDC event was transformed to domain event
		handler.TransformedEvents.Count.ShouldBe(1);
		var domainEvent = handler.TransformedEvents[0];
		_ = domainEvent.ShouldBeOfType<OrderCreatedDomainEvent>();

		var orderCreated = (OrderCreatedDomainEvent)domainEvent;
		orderCreated.OrderId.ShouldBe("ORD-001");
		orderCreated.TotalAmount.ShouldBe(150.00m);
		orderCreated.CustomerId.ShouldBe("CUST-123");
	}

	/// <summary>
	/// Tests that database column names are correctly mapped to domain event property names.
	/// Column: order_id > Property: OrderId (snake_case to PascalCase).
	/// </summary>
	[Fact]
	public async Task MapColumnToProperty()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new SimulatedDataChangeHandler("Products", executionLog);
		var pipeline = new CdcPipeline(executionLog, handler);

		// CDC uses snake_case column names typical of SQL databases
		var cdcEvent = cdcSource.EmitInsert("Products", new Dictionary<string, object?>
		{
			["product_id"] = "PROD-001",
			["product_name"] = "Widget X",
			["unit_price"] = 29.99m,
			["stock_quantity"] = 100,
			["is_active"] = true,
		});

		// Act
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - Column names were mapped to PascalCase properties
		handler.TransformedEvents.Count.ShouldBe(1);
		var domainEvent = handler.TransformedEvents[0];
		_ = domainEvent.ShouldBeOfType<ProductCreatedDomainEvent>();

		var productCreated = (ProductCreatedDomainEvent)domainEvent;
		productCreated.ProductId.ShouldBe("PROD-001");     // product_id -> ProductId
		productCreated.ProductName.ShouldBe("Widget X");   // product_name -> ProductName
		productCreated.UnitPrice.ShouldBe(29.99m);         // unit_price -> UnitPrice
		productCreated.StockQuantity.ShouldBe(100);        // stock_quantity -> StockQuantity
		productCreated.IsActive.ShouldBeTrue();            // is_active -> IsActive
	}

	/// <summary>
	/// Tests that NULL column values are handled correctly during transformation.
	/// NULL columns > nullable properties or default values.
	/// </summary>
	[Fact]
	public async Task HandleNullValues()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new SimulatedDataChangeHandler("Customers", executionLog);
		var pipeline = new CdcPipeline(executionLog, handler);

		// Some columns have NULL values
		var cdcEvent = cdcSource.EmitInsert("Customers", new Dictionary<string, object?>
		{
			["customer_id"] = "CUST-001",
			["name"] = "John Doe",
			["email"] = null,               // NULL email
			["phone"] = null,               // NULL phone
			["loyalty_points"] = null,      // NULL numeric
		});

		// Act
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - NULL values were handled properly
		handler.TransformedEvents.Count.ShouldBe(1);
		var domainEvent = handler.TransformedEvents[0];
		_ = domainEvent.ShouldBeOfType<CustomerCreatedDomainEvent>();

		var customerCreated = (CustomerCreatedDomainEvent)domainEvent;
		customerCreated.CustomerId.ShouldBe("CUST-001");
		customerCreated.Name.ShouldBe("John Doe");
		customerCreated.Email.ShouldBeNull();
		customerCreated.Phone.ShouldBeNull();
		customerCreated.LoyaltyPoints.ShouldBe(0); // Default for nullable int
	}

	/// <summary>
	/// Tests that SQL types are correctly converted to .NET types during transformation.
	/// SQL decimal(18,2) > .NET decimal, SQL datetime2 > .NET DateTimeOffset.
	/// </summary>
	[Fact]
	public async Task HandleTypeConversions()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new SimulatedDataChangeHandler("Invoices", executionLog);
		var pipeline = new CdcPipeline(executionLog, handler);

		// Various SQL types that need conversion
		var invoiceDate = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
		var cdcEvent = cdcSource.EmitInsertWithTypes("Invoices", new Dictionary<string, (object? Value, Type DataType)>
		{
			["invoice_id"] = ("INV-001", typeof(string)),
			["amount"] = (12345.67m, typeof(decimal)),           // SQL decimal(18,2)
			["invoice_date"] = (invoiceDate, typeof(DateTimeOffset)), // SQL datetime2
			["quantity"] = (42, typeof(int)),                    // SQL int
			["is_paid"] = (true, typeof(bool)),                  // SQL bit
			["tax_rate"] = (0.21, typeof(double)),               // SQL float
		});

		// Act
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);

		// Assert - Types were converted correctly
		handler.TransformedEvents.Count.ShouldBe(1);
		var domainEvent = handler.TransformedEvents[0];
		_ = domainEvent.ShouldBeOfType<InvoiceCreatedDomainEvent>();

		var invoiceCreated = (InvoiceCreatedDomainEvent)domainEvent;
		invoiceCreated.InvoiceId.ShouldBe("INV-001");
		invoiceCreated.Amount.ShouldBe(12345.67m);
		invoiceCreated.InvoiceDate.ShouldBe(invoiceDate);
		invoiceCreated.Quantity.ShouldBe(42);
		invoiceCreated.IsPaid.ShouldBeTrue();
		invoiceCreated.TaxRate.ShouldBe(0.21, tolerance: 0.001);
	}

	/// <summary>
	/// Tests that CDC events are enriched with metadata during transformation.
	/// CDC event > Domain event + CorrelationId, Timestamp, SourceTable.
	/// </summary>
	[Fact]
	public async Task EnrichEventWithMetadata()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cdcSource = new SimulatedCdcSource();
		var handler = new SimulatedDataChangeHandler("Shipments", executionLog);
		var pipeline = new CdcPipeline(executionLog, handler);

		var correlationId = Guid.NewGuid();
		pipeline.SetCorrelationId(correlationId);

		var cdcEvent = cdcSource.EmitInsert("Shipments", new Dictionary<string, object?>
		{
			["shipment_id"] = "SHIP-001",
			["destination"] = "New York",
		});

		// Act
		var beforeProcessing = DateTimeOffset.UtcNow;
		await pipeline.ProcessCdcEventAsync(cdcEvent, CancellationToken.None).ConfigureAwait(true);
		var afterProcessing = DateTimeOffset.UtcNow;

		// Assert - Event was enriched with metadata
		handler.TransformedEvents.Count.ShouldBe(1);
		var domainEvent = handler.TransformedEvents[0];
		_ = domainEvent.ShouldBeOfType<ShipmentCreatedDomainEvent>();

		var shipmentCreated = (ShipmentCreatedDomainEvent)domainEvent;
		shipmentCreated.ShipmentId.ShouldBe("SHIP-001");

		// Metadata assertions
		_ = shipmentCreated.Metadata.ShouldNotBeNull();
		shipmentCreated.Metadata.CorrelationId.ShouldBe(correlationId);
		shipmentCreated.Metadata.SourceTable.ShouldBe("Shipments");
		shipmentCreated.Metadata.Timestamp.ShouldBeInRange(beforeProcessing, afterProcessing);
	}

	#region Test Infrastructure

	internal enum SimulatedDataChangeType
	{
		Unknown = 0,
		Insert = 1,
		Update = 2,
		Delete = 3,
	}

	internal interface IDomainEventBase
	{
		EventMetadata? Metadata { get; set; }
	}

	/// <summary>
	/// Execution log to track CDC processing steps.
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
	/// Simulated CDC source that emits controlled DataChangeEvent objects.
	/// </summary>
	internal sealed class SimulatedCdcSource
	{
		private int _sequenceNumber;

		public SimulatedDataChangeEvent EmitInsert(string tableName, Dictionary<string, object?> columns)
		{
			var lsn = BitConverter.GetBytes(++_sequenceNumber);
			var changes = columns.Select(kvp => new SimulatedDataChange
			{
				ColumnName = kvp.Key,
				OldValue = null,
				NewValue = kvp.Value,
				DataType = kvp.Value?.GetType() ?? typeof(object),
			}).ToList();

			return new SimulatedDataChangeEvent
			{
				Lsn = lsn,
				SeqVal = lsn,
				CommitTime = DateTime.UtcNow,
				TableName = tableName,
				ChangeType = SimulatedDataChangeType.Insert,
				Changes = changes,
			};
		}

		public SimulatedDataChangeEvent EmitInsertWithTypes(string tableName, Dictionary<string, (object? Value, Type DataType)> columns)
		{
			var lsn = BitConverter.GetBytes(++_sequenceNumber);
			var changes = columns.Select(kvp => new SimulatedDataChange
			{
				ColumnName = kvp.Key,
				OldValue = null,
				NewValue = kvp.Value.Value,
				DataType = kvp.Value.DataType,
			}).ToList();

			return new SimulatedDataChangeEvent
			{
				Lsn = lsn,
				SeqVal = lsn,
				CommitTime = DateTime.UtcNow,
				TableName = tableName,
				ChangeType = SimulatedDataChangeType.Insert,
				Changes = changes,
			};
		}

		public SimulatedDataChangeEvent EmitUpdate(
			string tableName,
			Dictionary<string, object?> beforeValues,
			Dictionary<string, object?> afterValues)
		{
			var lsn = BitConverter.GetBytes(++_sequenceNumber);
			var changes = afterValues.Select(kvp => new SimulatedDataChange
			{
				ColumnName = kvp.Key,
				OldValue = beforeValues.GetValueOrDefault(kvp.Key),
				NewValue = kvp.Value,
				DataType = kvp.Value?.GetType() ?? typeof(object),
			}).ToList();

			return new SimulatedDataChangeEvent
			{
				Lsn = lsn,
				SeqVal = lsn,
				CommitTime = DateTime.UtcNow,
				TableName = tableName,
				ChangeType = SimulatedDataChangeType.Update,
				Changes = changes,
			};
		}

		public SimulatedDataChangeEvent EmitDelete(string tableName, Dictionary<string, object?> columns)
		{
			var lsn = BitConverter.GetBytes(++_sequenceNumber);
			var changes = columns.Select(kvp => new SimulatedDataChange
			{
				ColumnName = kvp.Key,
				OldValue = kvp.Value,
				NewValue = null,
				DataType = kvp.Value?.GetType() ?? typeof(object),
			}).ToList();

			return new SimulatedDataChangeEvent
			{
				Lsn = lsn,
				SeqVal = lsn,
				CommitTime = DateTime.UtcNow,
				TableName = tableName,
				ChangeType = SimulatedDataChangeType.Delete,
				Changes = changes,
			};
		}
	}

	/// <summary>
	/// Simulated data change handler that transforms CDC events to domain events.
	/// </summary>
	internal sealed class SimulatedDataChangeHandler
	{
		private readonly ExecutionLog _log;

		public SimulatedDataChangeHandler(string tableName, ExecutionLog log)
		{
			TableNames = [tableName];
			_log = log;
		}

		public string[] TableNames { get; }
		public List<IDomainEventBase> TransformedEvents { get; } = [];

		public Task HandleAsync(SimulatedDataChangeEvent changeEvent, CancellationToken cancellationToken)
		{
			_log.Log($"Handler:{changeEvent.TableName}:{changeEvent.ChangeType}");

			var domainEvent = TransformToDomainEvent(changeEvent);

			// Enrich with metadata from CDC event
			domainEvent.Metadata = new EventMetadata
			{
				CorrelationId = changeEvent.CorrelationId,
				SourceTable = changeEvent.TableName,
				Timestamp = changeEvent.ProcessedAt,
			};

			TransformedEvents.Add(domainEvent);

			_log.Log($"Transformed:{domainEvent.GetType().Name}");
			return Task.CompletedTask;
		}

		private static IDomainEventBase TransformToDomainEvent(SimulatedDataChangeEvent changeEvent)
		{
			// Convert snake_case column names to PascalCase and build domain event
			var columns = changeEvent.Changes
				.ToDictionary(
					c => ConvertToPascalCase(c.ColumnName),
					c => c.NewValue ?? c.OldValue);

			return changeEvent.TableName switch
			{
				"Orders" => new OrderCreatedDomainEvent
				{
					OrderId = GetValue<string>(columns, "OrderId") ?? string.Empty,
					TotalAmount = GetValue<decimal>(columns, "TotalAmount"),
					CustomerId = GetValue<string>(columns, "CustomerId") ?? string.Empty,
				},
				"Products" => new ProductCreatedDomainEvent
				{
					ProductId = GetValue<string>(columns, "ProductId") ?? string.Empty,
					ProductName = GetValue<string>(columns, "ProductName") ?? string.Empty,
					UnitPrice = GetValue<decimal>(columns, "UnitPrice"),
					StockQuantity = GetValue<int>(columns, "StockQuantity"),
					IsActive = GetValue<bool>(columns, "IsActive"),
				},
				"Customers" => new CustomerCreatedDomainEvent
				{
					CustomerId = GetValue<string>(columns, "CustomerId") ?? string.Empty,
					Name = GetValue<string>(columns, "Name") ?? string.Empty,
					Email = GetValue<string>(columns, "Email"),
					Phone = GetValue<string>(columns, "Phone"),
					LoyaltyPoints = GetValue<int>(columns, "LoyaltyPoints"),
				},
				"Invoices" => new InvoiceCreatedDomainEvent
				{
					InvoiceId = GetValue<string>(columns, "InvoiceId") ?? string.Empty,
					Amount = GetValue<decimal>(columns, "Amount"),
					InvoiceDate = GetValue<DateTimeOffset>(columns, "InvoiceDate"),
					Quantity = GetValue<int>(columns, "Quantity"),
					IsPaid = GetValue<bool>(columns, "IsPaid"),
					TaxRate = GetValue<double>(columns, "TaxRate"),
				},
				"Shipments" => new ShipmentCreatedDomainEvent
				{
					ShipmentId = GetValue<string>(columns, "ShipmentId") ?? string.Empty,
					Destination = GetValue<string>(columns, "Destination") ?? string.Empty,
				},
				_ => throw new InvalidOperationException($"Unknown table: {changeEvent.TableName}"),
			};
		}

		private static T GetValue<T>(Dictionary<string, object?> columns, string key)
		{
			if (columns.TryGetValue(key, out var value) && value is T typedValue)
			{
				return typedValue;
			}

			return default!;
		}

		private static string ConvertToPascalCase(string snakeCase)
		{
			if (string.IsNullOrEmpty(snakeCase))
			{
				return snakeCase;
			}

			var parts = snakeCase.Split('_');
			return string.Concat(parts.Select(p =>
				char.ToUpperInvariant(p[0]) + p[1..]));
		}
	}

	/// <summary>
	/// CDC pipeline that processes events through handlers.
	/// </summary>
	internal sealed class CdcPipeline
	{
		private readonly ExecutionLog _log;
		private readonly SimulatedDataChangeHandler _handler;
		private Guid _correlationId = Guid.NewGuid();

		public CdcPipeline(ExecutionLog log, SimulatedDataChangeHandler handler)
		{
			_log = log;
			_handler = handler;
		}

		public void SetCorrelationId(Guid correlationId) => _correlationId = correlationId;

		public async Task ProcessCdcEventAsync(SimulatedDataChangeEvent cdcEvent, CancellationToken cancellationToken)
		{
			_log.Log($"Pipeline:Start:{cdcEvent.TableName}");

			// Enrich with metadata before processing
			cdcEvent.CorrelationId = _correlationId;
			cdcEvent.ProcessedAt = DateTimeOffset.UtcNow;

			if (_handler.TableNames.Contains(cdcEvent.TableName))
			{
				await _handler.HandleAsync(cdcEvent, cancellationToken).ConfigureAwait(false);
			}

			_log.Log($"Pipeline:End:{cdcEvent.TableName}");
		}
	}

	// Simulated CDC types (mirrors the real Excalibur.Data.SqlServer.Cdc types)
	internal sealed class SimulatedDataChange
	{
		public string ColumnName { get; init; } = string.Empty;
		public object? OldValue { get; init; }
		public object? NewValue { get; init; }
		public Type DataType { get; init; } = typeof(object);
	}

	internal sealed class SimulatedDataChangeEvent
	{
		public byte[] Lsn { get; init; } = [];
		public byte[] SeqVal { get; init; } = [];
		public DateTime CommitTime { get; init; }
		public string TableName { get; init; } = string.Empty;
		public SimulatedDataChangeType ChangeType { get; init; }
		public IList<SimulatedDataChange> Changes { get; init; } = [];
		public Guid CorrelationId { get; set; }
		public DateTimeOffset ProcessedAt { get; set; }
	}

	// Domain event types
	internal sealed class EventMetadata
	{
		public Guid CorrelationId { get; init; }
		public string SourceTable { get; init; } = string.Empty;
		public DateTimeOffset Timestamp { get; init; }
	}

	internal sealed class OrderCreatedDomainEvent : IDomainEventBase
	{
		public string OrderId { get; init; } = string.Empty;
		public decimal TotalAmount { get; init; }
		public string CustomerId { get; init; } = string.Empty;
		public EventMetadata? Metadata { get; set; }
	}

	internal sealed class ProductCreatedDomainEvent : IDomainEventBase
	{
		public string ProductId { get; init; } = string.Empty;
		public string ProductName { get; init; } = string.Empty;
		public decimal UnitPrice { get; init; }
		public int StockQuantity { get; init; }
		public bool IsActive { get; init; }
		public EventMetadata? Metadata { get; set; }
	}

	internal sealed class CustomerCreatedDomainEvent : IDomainEventBase
	{
		public string CustomerId { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public string? Email { get; init; }
		public string? Phone { get; init; }
		public int LoyaltyPoints { get; init; }
		public EventMetadata? Metadata { get; set; }
	}

	internal sealed class InvoiceCreatedDomainEvent : IDomainEventBase
	{
		public string InvoiceId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public DateTimeOffset InvoiceDate { get; init; }
		public int Quantity { get; init; }
		public bool IsPaid { get; init; }
		public double TaxRate { get; init; }
		public EventMetadata? Metadata { get; set; }
	}

	internal sealed class ShipmentCreatedDomainEvent : IDomainEventBase
	{
		public string ShipmentId { get; init; } = string.Empty;
		public string Destination { get; init; } = string.Empty;
		public EventMetadata? Metadata { get; set; }
	}

	#endregion Test Infrastructure
}
