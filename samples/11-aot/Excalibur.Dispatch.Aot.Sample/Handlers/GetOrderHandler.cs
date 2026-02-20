// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Aot.Sample.Messages;

namespace Excalibur.Dispatch.Aot.Sample.Handlers;

/// <summary>
/// Handles order query requests.
/// </summary>
/// <remarks>
/// Demonstrates AOT-compatible query handling with nullable return types.
/// </remarks>
public sealed class GetOrderHandler : IActionHandler<GetOrderQuery, OrderDto?>
{
	// In-memory store for demo purposes
	private static readonly Dictionary<Guid, OrderDto> _orders = new();
	private static readonly object _lock = new();

	/// <inheritdoc />
	public Task<OrderDto?> HandleAsync(GetOrderQuery action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		Console.WriteLine($"[GetOrderHandler] Looking up order {action.OrderId}");

		lock (_lock)
		{
			_ = _orders.TryGetValue(action.OrderId, out var order);
			Console.WriteLine(order != null
				? $"[GetOrderHandler] Found order for customer {order.CustomerId}"
				: $"[GetOrderHandler] Order not found");
			return Task.FromResult(order);
		}
	}

	/// <summary>
	/// Adds an order to the in-memory store (for demo purposes).
	/// </summary>
	/// <param name="order">The order to add.</param>
	internal static void AddOrder(OrderDto order)
	{
		lock (_lock)
		{
			_orders[order.Id] = order;
		}
	}
}
