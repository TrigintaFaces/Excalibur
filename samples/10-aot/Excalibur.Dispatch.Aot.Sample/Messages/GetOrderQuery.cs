// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Aot.Sample.Messages;

/// <summary>
/// Query to retrieve an order by ID.
/// Demonstrates AOT-compatible query dispatch.
/// </summary>
public sealed record GetOrderQuery : IDispatchAction<OrderDto>
{
	/// <summary>
	/// Gets or initializes the order ID to retrieve.
	/// </summary>
	public required Guid OrderId { get; init; }
}

/// <summary>
/// Read model representing an order.
/// </summary>
public sealed record OrderDto
{
	/// <summary>
	/// Gets or initializes the order ID.
	/// </summary>
	public required Guid Id { get; init; }

	/// <summary>
	/// Gets or initializes the customer ID.
	/// </summary>
	public required string CustomerId { get; init; }

	/// <summary>
	/// Gets or initializes the order status.
	/// </summary>
	public required string Status { get; init; }

	/// <summary>
	/// Gets or initializes the total amount.
	/// </summary>
	public required decimal TotalAmount { get; init; }

	/// <summary>
	/// Gets or initializes the order items.
	/// </summary>
	public required IReadOnlyList<OrderItem> Items { get; init; }

	/// <summary>
	/// Gets or initializes when the order was created.
	/// </summary>
	public required DateTimeOffset CreatedAt { get; init; }
}
