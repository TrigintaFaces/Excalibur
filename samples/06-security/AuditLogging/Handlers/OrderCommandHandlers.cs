// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using AuditLoggingSample.Messages;

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Logging;

namespace AuditLoggingSample.Handlers;

/// <summary>
/// Handles <see cref="CreateOrderCommand" />.
/// </summary>
/// <remarks>
/// The handler body is deliberately trivial: what this sample demonstrates is the audit record the
/// pipeline writes AROUND the handler. The handler still has to exist — dispatching an action with no
/// registered handler throws, so a sample without one audits nothing.
/// </remarks>
public sealed class CreateOrderCommandHandler(ILogger<CreateOrderCommandHandler> logger)
	: IActionHandler<CreateOrderCommand>
{
	/// <inheritdoc />
	public Task HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		logger.LogInformation(
			"[CreateOrderCommandHandler] Order {OrderId} created for customer {CustomerId} ({TotalAmount:C}).",
			action.OrderId,
			action.CustomerId,
			action.TotalAmount);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="UpdateCustomerCommand" />.
/// </summary>
public sealed class UpdateCustomerCommandHandler(ILogger<UpdateCustomerCommandHandler> logger)
	: IActionHandler<UpdateCustomerCommand>
{
	/// <inheritdoc />
	public Task HandleAsync(UpdateCustomerCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		// Only the non-sensitive fields are logged here. The command also carries an email, a phone
		// number and a national identifier; those reach the audit trail through the audit middleware,
		// which redacts them, and must never be written to an application log verbatim.
		logger.LogInformation(
			"[UpdateCustomerCommandHandler] Customer {CustomerId} updated.",
			action.CustomerId);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handles <see cref="DeleteOrderCommand" />.
/// </summary>
public sealed class DeleteOrderCommandHandler(ILogger<DeleteOrderCommandHandler> logger)
	: IActionHandler<DeleteOrderCommand>
{
	/// <inheritdoc />
	public Task HandleAsync(DeleteOrderCommand action, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		logger.LogInformation(
			"[DeleteOrderCommandHandler] Order {OrderId} deleted by {DeletedBy}: {Reason}",
			action.OrderId,
			action.DeletedBy,
			action.Reason);

		return Task.CompletedTask;
	}
}
