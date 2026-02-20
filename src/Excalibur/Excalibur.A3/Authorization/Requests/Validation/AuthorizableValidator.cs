// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Validation;

using FluentValidation;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Validates that an object implementing <see cref="IRequireAuthorization" /> contains the necessary access token for authorization.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <remarks> Ensures that the access token is provided and is not empty. </remarks>
public sealed class AuthorizableValidator<TRequest> : RulesFor<TRequest, IAmAuthorizable>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizableValidator{TRequest}" /> class.
	/// </summary>
	public AuthorizableValidator() => _ = RuleFor(static x => x.AccessToken).NotEmpty();
}
