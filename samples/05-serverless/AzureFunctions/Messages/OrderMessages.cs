// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace AzureFunctionsSample.Messages;

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
/// Event raised when a report is generated.
/// </summary>
/// <param name="ReportId">The report identifier.</param>
/// <param name="ReportDate">The date of the report.</param>
/// <param name="GeneratedAt">When the report was generated.</param>
public sealed record ReportGeneratedEvent(
	string ReportId,
	DateOnly ReportDate,
	DateTimeOffset GeneratedAt) : IDispatchEvent;

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
	IReadOnlyList<OrderItem> Items);
