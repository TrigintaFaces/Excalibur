// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

using FluentValidation;

using FluentValidationSample.Commands;

namespace FluentValidationSample.Validators;

/// <summary>
/// Validator for UpdateProfileCommand demonstrating optional field validation.
/// </summary>
public sealed partial class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
	public UpdateProfileCommandValidator()
	{
		// User ID is required
		_ = RuleFor(x => x.UserId)
			.NotEmpty()
			.WithMessage("User ID is required")
			.Must(BeValidGuid)
			.WithMessage("User ID must be a valid GUID");

		// Display name is optional but has constraints when provided
		_ = RuleFor(x => x.DisplayName)
			.MinimumLength(2)
			.WithMessage("Display name must be at least 2 characters")
			.MaximumLength(100)
			.WithMessage("Display name cannot exceed 100 characters")
			.When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

		// Bio is optional with length limit
		_ = RuleFor(x => x.Bio)
			.MaximumLength(500)
			.WithMessage("Bio cannot exceed 500 characters")
			.When(x => !string.IsNullOrWhiteSpace(x.Bio));

		// Website URL validation when provided
		_ = RuleFor(x => x.WebsiteUrl)
			.Must(BeValidUrl)
			.WithMessage("Please enter a valid URL (e.g., https://example.com)")
			.When(x => !string.IsNullOrWhiteSpace(x.WebsiteUrl));

		// Phone number validation when provided
		_ = RuleFor(x => x.PhoneNumber)
			.Matches(PhoneNumberPattern())
			.WithMessage("Please enter a valid phone number")
			.When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

		// At least one field must be provided for update
		_ = RuleFor(x => x)
			.Must(HaveAtLeastOneFieldToUpdate)
			.WithMessage("At least one field must be provided for update")
			.WithName("Profile");
	}

	private static bool BeValidGuid(string value)
	{
		return Guid.TryParse(value, out _);
	}

	private static bool BeValidUrl(string? url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return true;
		}

		return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
			   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
	}

	private static bool HaveAtLeastOneFieldToUpdate(UpdateProfileCommand command)
	{
		return !string.IsNullOrWhiteSpace(command.DisplayName)
			   || !string.IsNullOrWhiteSpace(command.Bio)
			   || !string.IsNullOrWhiteSpace(command.WebsiteUrl)
			   || !string.IsNullOrWhiteSpace(command.PhoneNumber);
	}

	[GeneratedRegex(@"^[\+]?[(]?[0-9]{3}[)]?[-\s\.]?[0-9]{3}[-\s\.]?[0-9]{4,6}$")]
	private static partial Regex PhoneNumberPattern();
}
