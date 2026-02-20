// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace FluentValidationSample.Commands;

// ============================================================
// Commands for FluentValidation Demo
// ============================================================

/// <summary>
/// Command to create a new user account.
/// </summary>
public sealed record CreateUserCommand(
	string Username,
	string Email,
	string Password,
	int Age) : IDispatchAction;

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(
	string CustomerId,
	string ProductId,
	int Quantity,
	decimal UnitPrice,
	string ShippingAddress,
	string? PromoCode) : IDispatchAction;

/// <summary>
/// Command to update user profile.
/// </summary>
public sealed record UpdateProfileCommand(
	string UserId,
	string? DisplayName,
	string? Bio,
	string? WebsiteUrl,
	string? PhoneNumber) : IDispatchAction;

/// <summary>
/// Command with async validation requirements.
/// </summary>
public sealed record RegisterEmailCommand(
	string Email,
	bool AcceptTerms,
	string? ReferralCode) : IDispatchAction;
