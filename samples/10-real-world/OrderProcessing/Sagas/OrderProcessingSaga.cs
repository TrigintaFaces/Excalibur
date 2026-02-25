// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using OrderProcessingSample.Domain.Aggregates;
using OrderProcessingSample.Domain.Commands;
using OrderProcessingSample.ExternalServices;
using OrderProcessingSample.Handlers;

namespace OrderProcessingSample.Sagas;

// ============================================================================
// Order Processing Saga
// ============================================================================
// This saga orchestrates the order processing workflow:
// 1. Validate inventory availability
// 2. Reserve inventory
// 3. Process payment (with retry for transient failures)
// 4. Create shipment
// 5. Complete order
//
// If any step fails, compensating actions are executed.

/// <summary>
/// Orchestrates the order processing workflow (saga pattern).
/// </summary>
/// <remarks>
/// This is a synchronous saga for demonstration. In production, you would:
/// - Use Excalibur.Saga for persistent saga state
/// - Use the outbox pattern for reliable messaging
/// - Handle partial failures with compensation
/// </remarks>
public sealed class OrderProcessingSaga : IActionHandler<ProcessOrderCommand>
{
	private readonly InMemoryOrderStore _orderStore;
	private readonly IInventoryService _inventoryService;
	private readonly IPaymentService _paymentService;
	private readonly IShippingService _shippingService;

	public OrderProcessingSaga(
		InMemoryOrderStore orderStore,
		IInventoryService inventoryService,
		IPaymentService paymentService,
		IShippingService shippingService)
	{
		_orderStore = orderStore;
		_inventoryService = inventoryService;
		_paymentService = paymentService;
		_shippingService = shippingService;
	}

	public async Task HandleAsync(ProcessOrderCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine();
		Console.WriteLine($"  ┌─────────────────────────────────────────────────┐");
		Console.WriteLine($"  │ SAGA: Processing Order {action.OrderId.ToString()[..8]}                │");
		Console.WriteLine($"  └─────────────────────────────────────────────────┘");

		var order = _orderStore.GetById(action.OrderId)
					?? throw new InvalidOperationException($"Order {action.OrderId} not found");

		var sagaState = new SagaState();

		try
		{
			// Step 1: Validate inventory
			Console.WriteLine();
			Console.WriteLine("  [Step 1/5] Validating inventory...");
			await ValidateInventoryAsync(order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 2: Reserve inventory
			Console.WriteLine();
			Console.WriteLine("  [Step 2/5] Reserving inventory...");
			await ReserveInventoryAsync(order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 3: Process payment (with retry)
			Console.WriteLine();
			Console.WriteLine("  [Step 3/5] Processing payment...");
			await ProcessPaymentAsync(order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 4: Create shipment
			Console.WriteLine();
			Console.WriteLine("  [Step 4/5] Creating shipment...");
			await CreateShipmentAsync(order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 5: Complete (mark as shipped, awaiting delivery confirmation)
			Console.WriteLine();
			Console.WriteLine("  [Step 5/5] Saga complete - Order shipped!");
			Console.WriteLine($"    Tracking: {order.TrackingNumber} ({order.Carrier})");
		}
		catch (SagaFailedException ex)
		{
			Console.WriteLine();
			Console.WriteLine($"  [SAGA FAILED] {ex.Message}");
			Console.WriteLine("  [COMPENSATING] Executing compensating actions...");

			await CompensateAsync(order, sagaState, cancellationToken).ConfigureAwait(false);

			Console.WriteLine("  [COMPENSATED] Saga rolled back successfully");
		}
	}

	private async Task ValidateInventoryAsync(
		OrderAggregate order,
		SagaState state,
		CancellationToken cancellationToken)
	{
		var items = order.Items
			.Select(i => (i.ProductId, i.Quantity))
			.ToList();

		var result = await _inventoryService.ValidateInventoryAsync(items, cancellationToken)
			.ConfigureAwait(false);

		if (!result.IsValid)
		{
			order.MarkValidationFailed(string.Join(", ", result.UnavailableItems));
			_orderStore.Save(order);
			order.MarkEventsAsCommitted();

			throw new SagaFailedException(
				$"Inventory validation failed: {string.Join(", ", result.UnavailableItems)}");
		}

		order.MarkValidated();
		_orderStore.Save(order);
		order.MarkEventsAsCommitted();
		state.InventoryValidated = true;

		Console.WriteLine("    Inventory validation passed");
	}

	private async Task ReserveInventoryAsync(
		OrderAggregate order,
		SagaState state,
		CancellationToken cancellationToken)
	{
		var items = order.Items
			.Select(i => (i.ProductId, i.Quantity))
			.ToList();

		var reserved = await _inventoryService.ReserveInventoryAsync(order.Id, items, cancellationToken)
			.ConfigureAwait(false);

		if (!reserved)
		{
			throw new SagaFailedException("Failed to reserve inventory");
		}

		state.InventoryReserved = true;
		Console.WriteLine("    Inventory reserved");
	}

	private async Task ProcessPaymentAsync(
		OrderAggregate order,
		SagaState state,
		CancellationToken cancellationToken)
	{
		// Retry logic with exponential backoff
		const int maxRetries = 3;
		var attempt = 0;
		Exception? lastException = null;

		while (attempt < maxRetries)
		{
			attempt++;
			try
			{
				var result = await _paymentService.ProcessPaymentAsync(
						order.Id,
						order.CustomerId,
						order.TotalAmount,
						cancellationToken)
					.ConfigureAwait(false);

				if (!result.Success)
				{
					order.RecordPaymentFailure(result.ErrorMessage ?? "Unknown payment error");
					_orderStore.Save(order);
					order.MarkEventsAsCommitted();

					throw new SagaFailedException($"Payment failed: {result.ErrorMessage}");
				}

				order.RecordPayment(result.TransactionId, order.TotalAmount);
				_orderStore.Save(order);
				order.MarkEventsAsCommitted();
				state.PaymentProcessed = true;
				state.TransactionId = result.TransactionId;

				Console.WriteLine($"    Payment successful: {result.TransactionId}");
				return;
			}
			catch (HttpRequestException ex)
			{
				lastException = ex;
				if (attempt < maxRetries)
				{
					var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
					Console.WriteLine($"    Retry {attempt}/{maxRetries} after {delay.TotalMilliseconds}ms...");
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		order.RecordPaymentFailure($"Payment failed after {maxRetries} retries");
		_orderStore.Save(order);
		order.MarkEventsAsCommitted();

		throw new SagaFailedException(
			$"Payment failed after {maxRetries} retries: {lastException?.Message}");
	}

	private async Task CreateShipmentAsync(
		OrderAggregate order,
		SagaState state,
		CancellationToken cancellationToken)
	{
		var result = await _shippingService.CreateShipmentAsync(
				order.Id,
				order.ShippingAddress,
				cancellationToken)
			.ConfigureAwait(false);

		if (!result.Success)
		{
			throw new SagaFailedException($"Shipment failed: {result.ErrorMessage}");
		}

		order.Ship(result.TrackingNumber, result.Carrier);
		_orderStore.Save(order);
		order.MarkEventsAsCommitted();
		state.ShipmentCreated = true;

		Console.WriteLine($"    Shipment created: {result.TrackingNumber} via {result.Carrier}");
	}

	private async Task CompensateAsync(
		OrderAggregate order,
		SagaState state,
		CancellationToken cancellationToken)
	{
		// Compensate in reverse order

		// Note: We don't cancel shipments in this demo, but in production you would

		// Release inventory reservation
		if (state.InventoryReserved)
		{
			Console.WriteLine("    [Compensate] Releasing inventory reservation...");
			await _inventoryService.ReleaseInventoryAsync(order.Id, cancellationToken)
				.ConfigureAwait(false);
		}

		// Note: Payment refund would happen here in production
		if (state.PaymentProcessed)
		{
			Console.WriteLine($"    [Compensate] Would refund payment: {state.TransactionId}");
		}

		// Cancel the order if not already in a terminal state
		if (order.Status is not (OrderStatus.Cancelled or OrderStatus.ValidationFailed or OrderStatus.PaymentFailed))
		{
			order.Cancel("Saga compensation");
			_orderStore.Save(order);
			order.MarkEventsAsCommitted();
		}
	}

	/// <summary>
	/// Tracks saga state for compensation.
	/// </summary>
	private sealed class SagaState
	{
		public bool InventoryValidated { get; set; }
		public bool InventoryReserved { get; set; }
		public bool PaymentProcessed { get; set; }
		public string? TransactionId { get; set; }
		public bool ShipmentCreated { get; set; }
	}
}

/// <summary>
/// Exception thrown when a saga step fails.
/// </summary>
public sealed class SagaFailedException : Exception
{
	public SagaFailedException(string message) : base(message)
	{
	}
}
