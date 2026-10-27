// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using FluentValidation;

using FluentValidationSample.Commands;

namespace FluentValidationSample.Validators;

/// <summary>
/// Validator for CreateOrderCommand demonstrating conditional and complex rules.
/// </summary>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
	// Known promo codes for demo
	private static readonly HashSet<string> ValidPromoCodes =
		["SUMMER2026", "WELCOME10", "FREESHIP", "VIP25"];

	public CreateOrderCommandValidator()
	{
		// Customer ID validation
		_ = RuleFor(x => x.CustomerId)
			.NotEmpty()
			.WithMessage("Customer ID is required")
			.Must(BeValidGuid)
			.WithMessage("Customer ID must be a valid GUID");

		// Product ID validation
		_ = RuleFor(x => x.ProductId)
			.NotEmpty()
			.WithMessage("Product ID is required")
			.Must(BeValidGuid)
			.WithMessage("Product ID must be a valid GUID");

		// Quantity validation
		_ = RuleFor(x => x.Quantity)
			.GreaterThan(0)
			.WithMessage("Quantity must be greater than 0")
			.LessThanOrEqualTo(100)
			.WithMessage("Quantity cannot exceed 100 items per order");

		// Unit price validation
		_ = RuleFor(x => x.UnitPrice)
			.GreaterThan(0)
			.WithMessage("Unit price must be greater than 0")
			.LessThanOrEqualTo(10000)
			.WithMessage("Unit price cannot exceed $10,000");

		// Shipping address validation
		_ = RuleFor(x => x.ShippingAddress)
			.NotEmpty()
			.WithMessage("Shipping address is required")
			.MinimumLength(10)
			.WithMessage("Shipping address must be at least 10 characters")
			.MaximumLength(500)
			.WithMessage("Shipping address cannot exceed 500 characters");

		// Promo code validation (optional but must be valid if provided)
		_ = RuleFor(x => x.PromoCode)
			.Must(BeValidPromoCodeOrEmpty)
			.WithMessage("Invalid promo code")
			.When(x => !string.IsNullOrWhiteSpace(x.PromoCode));

		// Cross-field validation: order total
		_ = RuleFor(x => x)
			.Must(HaveReasonableOrderTotal)
			.WithMessage("Order total cannot exceed $50,000")
			.WithName("OrderTotal");
	}

	private static bool BeValidGuid(string value)
	{
		return Guid.TryParse(value, out _);
	}

	private static bool BeValidPromoCodeOrEmpty(string? promoCode)
	{
		if (string.IsNullOrWhiteSpace(promoCode))
		{
			return true;
		}

		return ValidPromoCodes.Contains(promoCode.ToUpperInvariant());
	}

	private static bool HaveReasonableOrderTotal(CreateOrderCommand command)
	{
		var total = command.Quantity * command.UnitPrice;
		return total <= 50000;
	}
}
