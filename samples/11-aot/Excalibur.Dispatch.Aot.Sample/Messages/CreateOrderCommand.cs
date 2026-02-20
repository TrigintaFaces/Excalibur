// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Aot.Sample.Messages;

/// <summary>
/// Command to create a new order.
/// Demonstrates AOT-compatible command dispatch with a return value.
/// </summary>
/// <remarks>
/// AOT Considerations:
/// - Record types work well with source-generated JSON serialization
/// - No reflection needed when using JsonSerializerContext
/// - Handler resolution is compile-time via source generators
/// </remarks>
public sealed record CreateOrderCommand : IDispatchAction<Guid>
{
	/// <summary>
	/// Gets or initializes the customer ID for the order.
	/// </summary>
	public required string CustomerId { get; init; }

	/// <summary>
	/// Gets or initializes the ordered items.
	/// </summary>
	public required IReadOnlyList<OrderItem> Items { get; init; }
}

/// <summary>
/// Represents an item in an order.
/// </summary>
/// <param name="Sku">The stock keeping unit identifier.</param>
/// <param name="Quantity">The quantity ordered.</param>
/// <param name="UnitPrice">The price per unit.</param>
public sealed record OrderItem(string Sku, int Quantity, decimal UnitPrice);
