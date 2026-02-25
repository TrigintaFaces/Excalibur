// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Validates that an object implementing <see cref="IRequireActivityAuthorization" /> contains the required resource-related
/// information for authorization.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <remarks> Ensures that the resource identifier and resource types are populated and valid. </remarks>
public sealed class AuthorizableForResourceValidator<TRequest> : RulesFor<TRequest, IRequireActivityAuthorization>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizableForResourceValidator{TRequest}" /> class.
	/// </summary>
	public AuthorizableForResourceValidator()
	{
		_ = RuleFor(static x => x.ResourceId).NotEmpty();
		_ = RuleFor(static x => x.ResourceTypes).NotEmpty();
		_ = RuleForEach(static x => x.ResourceTypes).NotEmpty();
	}
}
