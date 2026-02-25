// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions.Delivery;

using FluentValidationSample.Commands;

namespace FluentValidationSample.Handlers;

// ============================================================
// Command Handlers for FluentValidation Demo
// ============================================================

/// <summary>
/// Handler for CreateUserCommand.
/// </summary>
public sealed class CreateUserCommandHandler : IActionHandler<CreateUserCommand>
{
	public Task HandleAsync(CreateUserCommand action, CancellationToken cancellationToken)
	{
		// In a real application, this would create the user in the database
		Console.WriteLine($"  [Handler] Creating user: {action.Username} ({action.Email})");
		Console.WriteLine($"  [Handler] Age: {action.Age}");

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler for CreateOrderCommand.
/// </summary>
public sealed class CreateOrderCommandHandler : IActionHandler<CreateOrderCommand>
{
	public Task HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
	{
		var total = action.Quantity * action.UnitPrice;
		var hasPromo = !string.IsNullOrWhiteSpace(action.PromoCode);

		Console.WriteLine($"  [Handler] Creating order for customer: {action.CustomerId}");
		Console.WriteLine($"  [Handler] Product: {action.ProductId}, Qty: {action.Quantity}, Unit: ${action.UnitPrice:F2}");
		Console.WriteLine($"  [Handler] Total: ${total:F2}{(hasPromo ? $" (Promo: {action.PromoCode})" : "")}");
		Console.WriteLine($"  [Handler] Shipping to: {action.ShippingAddress}");

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler for UpdateProfileCommand.
/// </summary>
public sealed class UpdateProfileCommandHandler : IActionHandler<UpdateProfileCommand>
{
	public Task HandleAsync(UpdateProfileCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [Handler] Updating profile for user: {action.UserId}");

		if (!string.IsNullOrWhiteSpace(action.DisplayName))
		{
			Console.WriteLine($"  [Handler] - Display name: {action.DisplayName}");
		}

		if (!string.IsNullOrWhiteSpace(action.Bio))
		{
			Console.WriteLine($"  [Handler] - Bio: {action.Bio}");
		}

		if (!string.IsNullOrWhiteSpace(action.WebsiteUrl))
		{
			Console.WriteLine($"  [Handler] - Website: {action.WebsiteUrl}");
		}

		if (!string.IsNullOrWhiteSpace(action.PhoneNumber))
		{
			Console.WriteLine($"  [Handler] - Phone: {action.PhoneNumber}");
		}

		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler for RegisterEmailCommand.
/// </summary>
public sealed class RegisterEmailCommandHandler : IActionHandler<RegisterEmailCommand>
{
	public Task HandleAsync(RegisterEmailCommand action, CancellationToken cancellationToken)
	{
		Console.WriteLine($"  [Handler] Registering email: {action.Email}");
		Console.WriteLine($"  [Handler] Terms accepted: {action.AcceptTerms}");

		if (!string.IsNullOrWhiteSpace(action.ReferralCode))
		{
			Console.WriteLine($"  [Handler] Referral code: {action.ReferralCode}");
		}

		return Task.CompletedTask;
	}
}
