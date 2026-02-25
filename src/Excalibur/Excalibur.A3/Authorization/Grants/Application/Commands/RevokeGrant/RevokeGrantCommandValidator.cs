// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Validates the <see cref="RevokeGrantCommand" /> to ensure that all required fields are provided and valid.
/// </summary>
public sealed class RevokeGrantCommandValidator : RequestValidator<RevokeGrantCommand>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RevokeGrantCommandValidator" /> class.
	/// </summary>
	public RevokeGrantCommandValidator()
	{
		// Ensure the GrantType property is not empty
		_ = RuleFor(static x => x.GrantType).NotEmpty();

		// Ensure the Qualifier property is not empty
		_ = RuleFor(static x => x.Qualifier).NotEmpty();

		// Ensure the UserId property is not empty
		_ = RuleFor(static x => x.UserId).NotEmpty();
	}
}
