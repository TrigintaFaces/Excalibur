// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace GoogleCloudFunctionsSample.Messages;

/// <summary>
/// Event raised when an order is created.
/// </summary>
/// <param name="OrderId">The unique order identifier.</param>
/// <param name="CustomerId">The customer identifier.</param>
/// <param name="TotalAmount">The total order amount.</param>
/// <param name="CreatedAt">When the order was created.</param>
public sealed record OrderCreatedEvent(
	string OrderId,
	string CustomerId,
	decimal TotalAmount,
	DateTimeOffset CreatedAt) : IDispatchEvent;

/// <summary>
/// Event raised when a scheduled task completes.
/// </summary>
/// <param name="TaskId">The task identifier.</param>
/// <param name="TaskName">The task name.</param>
/// <param name="ExecutedAt">When the task was executed.</param>
public sealed record ScheduledTaskEvent(
	string TaskId,
	string TaskName,
	DateTimeOffset ExecutedAt) : IDispatchEvent;

/// <summary>
/// Represents an item in an order.
/// </summary>
/// <param name="ProductId">The product identifier.</param>
/// <param name="ProductName">The product name.</param>
/// <param name="Quantity">The quantity ordered.</param>
/// <param name="UnitPrice">The price per unit.</param>
public sealed record OrderItem(
	string ProductId,
	string ProductName,
	int Quantity,
	decimal UnitPrice);

/// <summary>
/// Request payload for creating an order.
/// </summary>
/// <param name="OrderId">The unique order identifier.</param>
/// <param name="CustomerId">The customer identifier.</param>
/// <param name="TotalAmount">The total order amount.</param>
/// <param name="Items">The order items.</param>
public sealed record CreateOrderRequest(
	string OrderId,
	string CustomerId,
	decimal TotalAmount,
	IReadOnlyList<OrderItem>? Items);
