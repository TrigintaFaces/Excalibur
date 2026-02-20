// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

using GettingStarted.Messages;

namespace GettingStarted.Handlers;

/// <summary>
/// In-memory order store for demonstration purposes.
/// </summary>
/// <remarks>
/// This service uses [AutoRegister] for compile-time service registration.
/// The source generator will create registration code at build time,
/// eliminating the need for runtime reflection.
///
/// In a real application, this would be replaced with a proper database.
/// </remarks>
[AutoRegister(Lifetime = ServiceLifetime.Singleton)]
public class OrderStore : IOrderStore
{
	private readonly ConcurrentDictionary<Guid, OrderDetails> _orders = new();

	/// <inheritdoc />
	public Guid CreateOrder(string productId, int quantity)
	{
		var order = new OrderDetails(
			Id: Guid.NewGuid(),
			ProductId: productId,
			Quantity: quantity,
			Status: "Pending",
			CreatedAt: DateTimeOffset.UtcNow);

		_orders[order.Id] = order;
		return order.Id;
	}

	/// <inheritdoc />
	public OrderDetails? GetOrder(Guid orderId)
	{
		return _orders.TryGetValue(orderId, out var order) ? order : null;
	}

	/// <inheritdoc />
	public void UpdateOrderStatus(Guid orderId, string status)
	{
		if (_orders.TryGetValue(orderId, out var order))
		{
			_orders[orderId] = order with { Status = status };
		}
	}
}

/// <summary>
/// Interface for order storage operations.
/// </summary>
public interface IOrderStore
{
	/// <summary>
	/// Creates a new order.
	/// </summary>
	Guid CreateOrder(string productId, int quantity);

	/// <summary>
	/// Gets an order by ID.
	/// </summary>
	OrderDetails? GetOrder(Guid orderId);

	/// <summary>
	/// Updates the status of an order.
	/// </summary>
	void UpdateOrderStatus(Guid orderId, string status);
}
