// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

using OrderProcessingSample.Domain.Aggregates;
using OrderProcessingSample.Domain.Commands;
using OrderProcessingSample.ExternalServices;

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
// Each step loads the aggregate fresh from the repository (load-mutate-save),
// which is the correct production pattern for saga steps.
// If any step fails, compensating actions are executed.

/// <summary>
/// Orchestrates the order processing workflow (saga pattern).
/// </summary>
/// <remarks>
/// Each saga step follows the load-mutate-save pattern via IEventSourcedRepository.
/// In production, you would additionally use:
/// - Excalibur.Saga for persistent saga state
/// - The outbox pattern for reliable messaging
/// - Compensation with durable state
/// </remarks>
public sealed class OrderProcessingSaga : IActionHandler<ProcessOrderCommand>
{
	private readonly IEventSourcedRepository<OrderAggregate, Guid> _repository;
	private readonly IInventoryService _inventoryService;
	private readonly IPaymentService _paymentService;
	private readonly IShippingService _shippingService;

	public OrderProcessingSaga(
		IEventSourcedRepository<OrderAggregate, Guid> repository,
		IInventoryService inventoryService,
		IPaymentService paymentService,
		IShippingService shippingService)
	{
		_repository = repository ?? throw new ArgumentNullException(nameof(repository));
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

		var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false)
					?? throw new InvalidOperationException($"Order {action.OrderId} not found");

		var sagaState = new SagaState();

		try
		{
			// Step 1: Validate inventory
			Console.WriteLine();
			Console.WriteLine("  [Step 1/5] Validating inventory...");
			await ValidateInventoryAsync(action.OrderId, order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 2: Reserve inventory
			Console.WriteLine();
			Console.WriteLine("  [Step 2/5] Reserving inventory...");
			await ReserveInventoryAsync(action.OrderId, order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 3: Process payment (with retry)
			Console.WriteLine();
			Console.WriteLine("  [Step 3/5] Processing payment...");
			await ProcessPaymentAsync(action.OrderId, order, sagaState, cancellationToken).ConfigureAwait(false);

			// Step 4: Create shipment
			Console.WriteLine();
			Console.WriteLine("  [Step 4/5] Creating shipment...");
			order = await CreateShipmentAsync(action.OrderId, sagaState, cancellationToken).ConfigureAwait(false);

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

			await CompensateAsync(action.OrderId, sagaState, cancellationToken).ConfigureAwait(false);

			Console.WriteLine("  [COMPENSATED] Saga rolled back successfully");
		}
	}

	private async Task ValidateInventoryAsync(
		Guid orderId,
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
			// Load fresh for mutation
			order = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
				?? throw new InvalidOperationException($"Order {orderId} not found");

			order.MarkValidationFailed(string.Join(", ", result.UnavailableItems));
			await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

			throw new SagaFailedException(
				$"Inventory validation failed: {string.Join(", ", result.UnavailableItems)}");
		}

		// Load fresh for mutation
		order = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Order {orderId} not found");

		order.MarkValidated();
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
		state.InventoryValidated = true;

		Console.WriteLine("    Inventory validation passed");
	}

	private async Task ReserveInventoryAsync(
		Guid orderId,
		OrderAggregate order,
		SagaState state,
		CancellationToken cancellationToken)
	{
		var items = order.Items
			.Select(i => (i.ProductId, i.Quantity))
			.ToList();

		var reserved = await _inventoryService.ReserveInventoryAsync(orderId, items, cancellationToken)
			.ConfigureAwait(false);

		if (!reserved)
		{
			throw new SagaFailedException("Failed to reserve inventory");
		}

		state.InventoryReserved = true;
		Console.WriteLine("    Inventory reserved");
	}

	private async Task ProcessPaymentAsync(
		Guid orderId,
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
						orderId,
						order.CustomerId,
						order.TotalAmount,
						cancellationToken)
					.ConfigureAwait(false);

				if (!result.Success)
				{
					// Load fresh for mutation
					var freshOrder = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
						?? throw new InvalidOperationException($"Order {orderId} not found");

					freshOrder.RecordPaymentFailure(result.ErrorMessage ?? "Unknown payment error");
					await _repository.SaveAsync(freshOrder, cancellationToken).ConfigureAwait(false);

					throw new SagaFailedException($"Payment failed: {result.ErrorMessage}");
				}

				// Load fresh for mutation
				var orderForPayment = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
					?? throw new InvalidOperationException($"Order {orderId} not found");

				orderForPayment.RecordPayment(result.TransactionId, orderForPayment.TotalAmount);
				await _repository.SaveAsync(orderForPayment, cancellationToken).ConfigureAwait(false);
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

		// Load fresh for mutation
		var orderForFailure = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Order {orderId} not found");

		orderForFailure.RecordPaymentFailure($"Payment failed after {maxRetries} retries");
		await _repository.SaveAsync(orderForFailure, cancellationToken).ConfigureAwait(false);

		throw new SagaFailedException(
			$"Payment failed after {maxRetries} retries: {lastException?.Message}");
	}

	private async Task<OrderAggregate> CreateShipmentAsync(
		Guid orderId,
		SagaState state,
		CancellationToken cancellationToken)
	{
		var order = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Order {orderId} not found");

		var result = await _shippingService.CreateShipmentAsync(
				orderId,
				order.ShippingAddress,
				cancellationToken)
			.ConfigureAwait(false);

		if (!result.Success)
		{
			throw new SagaFailedException($"Shipment failed: {result.ErrorMessage}");
		}

		order.Ship(result.TrackingNumber, result.Carrier);
		await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
		state.ShipmentCreated = true;

		Console.WriteLine($"    Shipment created: {result.TrackingNumber} via {result.Carrier}");
		return order;
	}

	private async Task CompensateAsync(
		Guid orderId,
		SagaState state,
		CancellationToken cancellationToken)
	{
		// Compensate in reverse order

		// Note: We don't cancel shipments in this demo, but in production you would

		// Release inventory reservation
		if (state.InventoryReserved)
		{
			Console.WriteLine("    [Compensate] Releasing inventory reservation...");
			await _inventoryService.ReleaseInventoryAsync(orderId, cancellationToken)
				.ConfigureAwait(false);
		}

		// Note: Payment refund would happen here in production
		if (state.PaymentProcessed)
		{
			Console.WriteLine($"    [Compensate] Would refund payment: {state.TransactionId}");
		}

		// Cancel the order if not already in a terminal state
		var order = await _repository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
		if (order != null && order.Status is not (OrderStatus.Cancelled or OrderStatus.ValidationFailed or OrderStatus.PaymentFailed))
		{
			order.Cancel("Saga compensation");
			await _repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
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
