// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using FluentValidation;

using FluentValidationSample.Commands;

namespace FluentValidationSample.Validators;

/// <summary>
/// Validator for CreateUserCommand demonstrating basic validation rules.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
	public CreateUserCommandValidator()
	{
		// Username validation
		_ = RuleFor(x => x.Username)
			.NotEmpty()
			.WithMessage("Username is required")
			.MinimumLength(3)
			.WithMessage("Username must be at least 3 characters")
			.MaximumLength(50)
			.WithMessage("Username cannot exceed 50 characters")
			.Matches("^[a-zA-Z0-9_]+$")
			.WithMessage("Username can only contain letters, numbers, and underscores");

		// Email validation
		_ = RuleFor(x => x.Email)
			.NotEmpty()
			.WithMessage("Email is required")
			.EmailAddress()
			.WithMessage("A valid email address is required")
			.MaximumLength(255)
			.WithMessage("Email cannot exceed 255 characters");

		// Password validation
		_ = RuleFor(x => x.Password)
			.NotEmpty()
			.WithMessage("Password is required")
			.MinimumLength(8)
			.WithMessage("Password must be at least 8 characters")
			.MaximumLength(100)
			.WithMessage("Password cannot exceed 100 characters")
			.Matches("[A-Z]")
			.WithMessage("Password must contain at least one uppercase letter")
			.Matches("[a-z]")
			.WithMessage("Password must contain at least one lowercase letter")
			.Matches("[0-9]")
			.WithMessage("Password must contain at least one digit")
			.Matches("[^a-zA-Z0-9]")
			.WithMessage("Password must contain at least one special character");

		// Age validation
		_ = RuleFor(x => x.Age)
			.GreaterThanOrEqualTo(13)
			.WithMessage("You must be at least 13 years old to create an account")
			.LessThanOrEqualTo(120)
			.WithMessage("Please enter a valid age");
	}
}
