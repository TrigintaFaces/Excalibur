// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace OrderProcessingSample.ExternalServices;

// ============================================================================
// External Service Abstractions and Mock Implementations
// ============================================================================
// These simulate external services (payment, shipping, inventory) that would
// typically be called via HTTP/gRPC. The mock implementations demonstrate
// retry and failure scenarios.

/// <summary>
/// Payment service abstraction.
/// </summary>
public interface IPaymentService
{
	/// <summary>
	/// Processes a payment for the specified amount.
	/// </summary>
	Task<PaymentResult> ProcessPaymentAsync(
		Guid orderId,
		Guid customerId,
		decimal amount,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of a payment operation.
/// </summary>
public sealed record PaymentResult(
	bool Success,
	string? TransactionId,
	string? ErrorMessage);

/// <summary>
/// Shipping service abstraction.
/// </summary>
public interface IShippingService
{
	/// <summary>
	/// Creates a shipment for an order.
	/// </summary>
	Task<ShipmentResult> CreateShipmentAsync(
		Guid orderId,
		string address,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of a shipping operation.
/// </summary>
public sealed record ShipmentResult(
	bool Success,
	string? TrackingNumber,
	string? Carrier,
	string? ErrorMessage);

/// <summary>
/// Inventory service abstraction.
/// </summary>
public interface IInventoryService
{
	/// <summary>
	/// Validates that all items are available in inventory.
	/// </summary>
	Task<InventoryValidationResult> ValidateInventoryAsync(
		IReadOnlyList<(Guid ProductId, int Quantity)> items,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reserves inventory for an order.
	/// </summary>
	Task<bool> ReserveInventoryAsync(
		Guid orderId,
		IReadOnlyList<(Guid ProductId, int Quantity)> items,
		CancellationToken cancellationToken);

	/// <summary>
	/// Releases reserved inventory (for cancellation/failure).
	/// </summary>
	Task ReleaseInventoryAsync(
		Guid orderId,
		CancellationToken cancellationToken);
}

/// <summary>
/// Result of inventory validation.
/// </summary>
public sealed record InventoryValidationResult(
	bool IsValid,
	IReadOnlyList<string> UnavailableItems);

// ============================================================================
// Mock Implementations
// ============================================================================

/// <summary>
/// Mock payment service that simulates various scenarios.
/// </summary>
public sealed class MockPaymentService : IPaymentService
{
	private int _callCount;
	private readonly Random _random = new();

	/// <summary>
	/// Gets or sets whether to simulate transient failures (for retry demo).
	/// </summary>
	public bool SimulateTransientFailures { get; set; } = true;

	/// <summary>
	/// Gets or sets the probability of a transient failure (0-1).
	/// </summary>
	public double TransientFailureProbability { get; set; } = 0.3;

	public async Task<PaymentResult> ProcessPaymentAsync(
		Guid orderId,
		Guid customerId,
		decimal amount,
		CancellationToken cancellationToken)
	{
		_callCount++;
		Console.WriteLine($"    [PaymentService] Processing payment (attempt {_callCount}): ${amount:F2}");

		// Simulate network latency
		await Task.Delay(100, cancellationToken).ConfigureAwait(false);

		// Simulate transient failures (for retry demo)
		if (SimulateTransientFailures && _callCount <= 2 && _random.NextDouble() < TransientFailureProbability)
		{
			Console.WriteLine("    [PaymentService] Transient error - will retry");
			throw new HttpRequestException("Transient network error");
		}

		// Simulate payment validation
		if (amount > 10000)
		{
			return new PaymentResult(
				false,
				null,
				"Payment declined: Amount exceeds single transaction limit");
		}

		var transactionId = $"TXN-{orderId.ToString()[..8].ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMdd}";
		return new PaymentResult(true, transactionId, null);
	}

	/// <summary>
	/// Resets the call counter for testing.
	/// </summary>
	public void Reset() => _callCount = 0;
}

/// <summary>
/// Mock shipping service.
/// </summary>
public sealed class MockShippingService : IShippingService
{
	private static readonly string[] Carriers = ["FedEx", "UPS", "DHL", "USPS"];
	private readonly Random _random = new();

	public async Task<ShipmentResult> CreateShipmentAsync(
		Guid orderId,
		string address,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"    [ShippingService] Creating shipment to: {address}");

		// Simulate API call
		await Task.Delay(50, cancellationToken).ConfigureAwait(false);

		var carrier = Carriers[_random.Next(Carriers.Length)];
		var trackingNumber = $"{carrier[..2].ToUpperInvariant()}{_random.Next(100000000, 999999999)}";

		return new ShipmentResult(true, trackingNumber, carrier, null);
	}
}

/// <summary>
/// Mock inventory service.
/// </summary>
public sealed class MockInventoryService : IInventoryService
{
	private readonly HashSet<Guid> _reservations = [];

	/// <summary>
	/// Gets or sets product IDs that should be reported as unavailable.
	/// </summary>
	public HashSet<Guid> UnavailableProducts { get; } = [];

	public Task<InventoryValidationResult> ValidateInventoryAsync(
		IReadOnlyList<(Guid ProductId, int Quantity)> items,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"    [InventoryService] Validating inventory for {items.Count} items");

		var unavailable = items
			.Where(i => UnavailableProducts.Contains(i.ProductId))
			.Select(i => $"Product {i.ProductId.ToString()[..8]}: insufficient stock")
			.ToList();

		return Task.FromResult(new InventoryValidationResult(
			unavailable.Count == 0,
			unavailable));
	}

	public Task<bool> ReserveInventoryAsync(
		Guid orderId,
		IReadOnlyList<(Guid ProductId, int Quantity)> items,
		CancellationToken cancellationToken)
	{
		Console.WriteLine($"    [InventoryService] Reserving inventory for order {orderId.ToString()[..8]}");
		_ = _reservations.Add(orderId);
		return Task.FromResult(true);
	}

	public Task ReleaseInventoryAsync(Guid orderId, CancellationToken cancellationToken)
	{
		Console.WriteLine($"    [InventoryService] Releasing inventory for order {orderId.ToString()[..8]}");
		_ = _reservations.Remove(orderId);
		return Task.CompletedTask;
	}
}
