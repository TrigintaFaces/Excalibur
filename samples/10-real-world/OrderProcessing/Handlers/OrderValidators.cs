// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using FluentValidation;

using OrderProcessingSample.Domain.Commands;

namespace OrderProcessingSample.Handlers;

// ============================================================================
// Order Command Validators
// ============================================================================
// FluentValidation validators for order commands.

/// <summary>
/// Validator for CreateOrderCommand.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
	public CreateOrderCommandValidator()
	{
		_ = RuleFor(x => x.CustomerId)
			.NotEmpty()
			.WithMessage("Customer ID is required");

		_ = RuleFor(x => x.Items)
			.NotEmpty()
			.WithMessage("Order must have at least one item")
			.Must(items => items.All(i => i.Quantity > 0))
			.WithMessage("All items must have quantity > 0")
			.Must(items => items.All(i => i.UnitPrice >= 0))
			.WithMessage("All items must have non-negative prices");

		_ = RuleFor(x => x.ShippingAddress)
			.NotEmpty()
			.WithMessage("Shipping address is required")
			.MinimumLength(10)
			.WithMessage("Shipping address must be at least 10 characters")
			.MaximumLength(500)
			.WithMessage("Shipping address cannot exceed 500 characters");
	}
}

/// <summary>
/// Validator for ProcessOrderCommand.
/// </summary>
public sealed class ProcessOrderCommandValidator : AbstractValidator<ProcessOrderCommand>
{
	public ProcessOrderCommandValidator()
	{
		_ = RuleFor(x => x.OrderId)
			.NotEmpty()
			.WithMessage("Order ID is required");
	}
}

/// <summary>
/// Validator for CancelOrderCommand.
/// </summary>
public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
	public CancelOrderCommandValidator()
	{
		_ = RuleFor(x => x.OrderId)
			.NotEmpty()
			.WithMessage("Order ID is required");

		_ = RuleFor(x => x.Reason)
			.NotEmpty()
			.WithMessage("Cancellation reason is required")
			.MinimumLength(3)
			.WithMessage("Cancellation reason must be at least 3 characters");
	}
}

/// <summary>
/// Validator for ConfirmDeliveryCommand.
/// </summary>
public sealed class ConfirmDeliveryCommandValidator : AbstractValidator<ConfirmDeliveryCommand>
{
	public ConfirmDeliveryCommandValidator()
	{
		_ = RuleFor(x => x.OrderId)
			.NotEmpty()
			.WithMessage("Order ID is required");
	}
}
