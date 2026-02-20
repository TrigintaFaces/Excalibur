// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace GettingStarted.Messages;

/// <summary>
/// Query to retrieve order details.
/// </summary>
/// <remarks>
/// Queries are read operations that do not modify state. They typically:
/// - Have a noun or "Get" prefix in their name
/// - Return data without side effects
/// - Are handled by exactly one handler
/// </remarks>
/// <param name="OrderId">The order ID to retrieve.</param>
public record GetOrderQuery(Guid OrderId) : IDispatchAction<OrderDetails?>;

/// <summary>
/// Order details returned by GetOrderQuery.
/// </summary>
/// <param name="Id">The order identifier.</param>
/// <param name="ProductId">The product that was ordered.</param>
/// <param name="Quantity">The quantity ordered.</param>
/// <param name="Status">Current order status.</param>
/// <param name="CreatedAt">When the order was created.</param>
public record OrderDetails(
	Guid Id,
	string ProductId,
	int Quantity,
	string Status,
	DateTimeOffset CreatedAt);
