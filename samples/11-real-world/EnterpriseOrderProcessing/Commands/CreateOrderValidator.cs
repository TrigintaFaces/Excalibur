// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using FluentValidation;

namespace EnterpriseOrderProcessing.Commands;

/// <summary>
/// FluentValidation validator for <see cref="CreateOrderCommand"/>.
/// Demonstrates the validation layer in the enterprise command pipeline.
/// </summary>
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
	public CreateOrderValidator()
	{
		RuleFor(x => x.CustomerId)
			.NotEmpty()
			.WithMessage("Customer ID is required.");

		RuleFor(x => x.CustomerName)
			.NotEmpty()
			.WithMessage("Customer name is required.")
			.MaximumLength(200)
			.WithMessage("Customer name must not exceed 200 characters.");

		RuleFor(x => x.Lines)
			.NotEmpty()
			.WithMessage("At least one order line is required.");

		RuleForEach(x => x.Lines).ChildRules(line =>
		{
			line.RuleFor(l => l.ProductId)
				.NotEmpty()
				.WithMessage("Product ID is required.");

			line.RuleFor(l => l.Quantity)
				.GreaterThan(0)
				.WithMessage("Quantity must be greater than zero.");

			line.RuleFor(l => l.UnitPrice)
				.GreaterThan(0)
				.WithMessage("Unit price must be greater than zero.");
		});
	}
}
