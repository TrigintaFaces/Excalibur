// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using FluentValidation;

using FluentValidationSample.Commands;

namespace FluentValidationSample.Validators;

/// <summary>
/// Validator for RegisterEmailCommand demonstrating custom validation rules.
/// </summary>
/// <remarks>
/// Note: The Dispatch FluentValidatorResolver uses synchronous validation.
/// For async validation (database lookups, API calls), consider:
/// - Pre-validation in the handler before dispatch
/// - Custom middleware for async validation
/// - ValidateAsync in application layer before dispatch
/// </remarks>
public sealed class RegisterEmailCommandValidator : AbstractValidator<RegisterEmailCommand>
{
	// Simulated existing emails for demo
	private static readonly HashSet<string> ExistingEmails =
		["admin@example.com", "support@example.com", "info@example.com"];

	// Valid referral codes
	private static readonly HashSet<string> ValidReferralCodes =
		["REF2026", "FRIEND10", "PARTNER20"];

	public RegisterEmailCommandValidator()
	{
		// Email validation
		_ = RuleFor(x => x.Email)
			.NotEmpty()
			.WithMessage("Email is required")
			.EmailAddress()
			.WithMessage("A valid email address is required")
			.MaximumLength(255)
			.WithMessage("Email cannot exceed 255 characters")
			.Must(BeUniqueEmail)
			.WithMessage("This email is already registered");

		// Terms acceptance is required
		_ = RuleFor(x => x.AcceptTerms)
			.Equal(true)
			.WithMessage("You must accept the terms and conditions to register");

		// Referral code validation (optional but must be valid if provided)
		_ = RuleFor(x => x.ReferralCode)
			.Must(BeValidReferralCodeOrEmpty)
			.WithMessage("Invalid referral code")
			.When(x => !string.IsNullOrWhiteSpace(x.ReferralCode));
	}

	private static bool BeUniqueEmail(string email)
	{
		// In a real application, this would query the database synchronously
		// or you'd perform async validation before dispatch
		return !ExistingEmails.Contains(email.ToLowerInvariant());
	}

	private static bool BeValidReferralCodeOrEmpty(string? referralCode)
	{
		if (string.IsNullOrWhiteSpace(referralCode))
		{
			return true;
		}

		// In a real application, this might call an API synchronously
		// or validate against a cached list
		return ValidReferralCodes.Contains(referralCode.ToUpperInvariant());
	}
}
