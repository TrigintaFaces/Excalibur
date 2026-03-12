// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace EnterpriseOrderProcessing.Commands;

/// <summary>
/// Command to create a new order from the enterprise order processing pipeline.
/// Dispatched through the command pipeline with FluentValidation and Polly resilience.
/// </summary>
public sealed record CreateOrderCommand(
	Guid CustomerId,
	string CustomerName,
	IReadOnlyList<OrderLineItem> Lines) : IDispatchAction<Guid>;

/// <summary>
/// Represents a line item in a create order command.
/// </summary>
public sealed record OrderLineItem(
	string ProductId,
	int Quantity,
	decimal UnitPrice);
