// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Grants;

/// <summary>
/// Validates the <see cref="RevokeAllGrantsCommand" /> to ensure all required properties are provided.
/// </summary>
public sealed class RevokeAllGrantsCommandValidator : RequestValidator<RevokeAllGrantsCommand>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RevokeAllGrantsCommandValidator" /> class.
	/// </summary>
	/// <remarks>
	/// Adds validation rules for the <see cref="RevokeAllGrantsCommand" /> to ensure that the
	/// <see cref="RevokeAllGrantsCommand.UserId" /> and <see cref="RevokeAllGrantsCommand.FullName" /> properties are not empty.
	/// </remarks>
	public RevokeAllGrantsCommandValidator()
	{
		// Validate that the FullName property is not empty
		_ = RuleFor(static x => x.FullName).NotEmpty();

		// Validate that the UserId property is not empty
		_ = RuleFor(static x => x.UserId).NotEmpty();
	}
}
