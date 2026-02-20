// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

using OrderProcessingSample.Domain.Events;

namespace OrderProcessingSample.Domain.Commands;

// ============================================================================
// Order Commands
// ============================================================================
// Commands represent user intentions. They are validated before processing
// and may be rejected. Each command maps to one or more domain events.

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(
	Guid CustomerId,
	IReadOnlyList<OrderLineItem> Items,
	string ShippingAddress) : IDispatchAction;

/// <summary>
/// Command to process an order (validate → pay → ship flow).
/// This is the saga entry point that orchestrates the full workflow.
/// </summary>
public sealed record ProcessOrderCommand(Guid OrderId) : IDispatchAction;

/// <summary>
/// Command to cancel an order.
/// </summary>
public sealed record CancelOrderCommand(
	Guid OrderId,
	string Reason) : IDispatchAction;

/// <summary>
/// Command to confirm order delivery (marks as complete).
/// </summary>
public sealed record ConfirmDeliveryCommand(Guid OrderId) : IDispatchAction;
