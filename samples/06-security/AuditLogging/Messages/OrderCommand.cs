// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace AuditLoggingSample.Messages;

/// <summary>
/// Command to create a new order. Demonstrates audit logging of command execution.
/// </summary>
public sealed record CreateOrderCommand(
	string OrderId,
	string CustomerId,
	string CustomerEmail,
	decimal TotalAmount,
	string CreditCardLast4) : IDispatchAction;

/// <summary>
/// Command to delete an order. Demonstrates audit logging of sensitive operations.
/// </summary>
public sealed record DeleteOrderCommand(
	string OrderId,
	string Reason,
	string DeletedBy) : IDispatchAction;

/// <summary>
/// Command to update customer information. Demonstrates PII redaction in audit logs.
/// </summary>
public sealed record UpdateCustomerCommand(
	string CustomerId,
	string Name,
	string Email,
	string PhoneNumber,
	string SocialSecurityNumber) : IDispatchAction;
