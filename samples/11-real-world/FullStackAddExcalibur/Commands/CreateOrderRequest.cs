// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace FullStackAddExcalibur.Commands;

/// <summary>
/// Request body for <c>POST /orders</c>.
/// </summary>
public sealed record CreateOrderRequest(
	string ExternalOrderId,
	Guid CustomerId,
	string CustomerExternalId,
	IReadOnlyList<CreateOrderLineItemRequest> LineItems,
	DateTime? OrderDate = null);

/// <summary>
/// Line item payload for <c>POST /orders</c>.
/// </summary>
public sealed record CreateOrderLineItemRequest(
	string ProductName,
	int Quantity,
	decimal UnitPrice);
