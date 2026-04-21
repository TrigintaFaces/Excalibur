// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Validates the <see cref="AddGrantCommand" /> to ensure it contains valid data before processing.
/// </summary>
public sealed class AddGrantCommandValidator : RequestValidator<AddGrantCommand>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AddGrantCommandValidator" /> class.
	/// </summary>
	public AddGrantCommandValidator()
	{
		// Ensure ExpiresOn is either null or a future timestamp.
		_ = RuleFor(static x => x.ExpiresOn)
			.Must(static x => x == null || x.GetValueOrDefault().ToUniversalTime() >= DateTimeOffset.UtcNow)
			.WithMessage("{propertyName} must be null or in the future.");

		// Validate that FullName is not empty.
		_ = RuleFor(static x => x.FullName).NotEmpty();

		// Validate that GrantType is not empty.
		_ = RuleFor(static x => x.GrantType).NotEmpty();

		// Validate that Qualifier is not empty.
		_ = RuleFor(static x => x.Qualifier).NotEmpty();

		// Validate wildcard patterns in Qualifier when present.
		_ = RuleFor(static x => x.Qualifier)
			.Must(static (command, qualifier) =>
			{
				if (qualifier is null || !qualifier.Contains('*', StringComparison.Ordinal))
				{
					return true;
				}

				return GrantScope.Validate(
					command.TenantId ?? "*",
					command.GrantType,
					qualifier,
					out _);
			})
			.WithMessage(static (command, qualifier) =>
			{
				GrantScope.Validate(
					command.TenantId ?? "*",
					command.GrantType,
					qualifier!,
					out var error);
				return error ?? "Invalid wildcard pattern.";
			})
			.When(static x => x.Qualifier is not null && x.Qualifier.Contains('*', StringComparison.Ordinal));

		// Validate that UserId is not empty.
		_ = RuleFor(static x => x.UserId).NotEmpty();
	}
}
