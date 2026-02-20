// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Application.Requests.Validation;

namespace Excalibur.A3.Authorization.Requests;

/// <summary>
/// Provides a base validator for requests that may be authorizable.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <remarks>
/// This validator automatically includes other relevant validators depending on the interfaces implemented by <typeparamref name="TRequest" />:
/// - If <typeparamref name="TRequest" /> implements <see cref="IRequireAuthorization" />, the <see cref="AuthorizableValidator{TRequest}" />
/// is included.
/// - If <typeparamref name="TRequest" /> implements <see cref="IRequireActivityAuthorization" />, the
/// <see cref="AuthorizableForResourceValidator{TRequest}" /> is included.
/// </remarks>
public abstract class AuthorizableTypeRequestValidator<TRequest> : RequestValidator<TRequest>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AuthorizableTypeRequestValidator{TRequest}" /> class.
	/// </summary>
	protected AuthorizableTypeRequestValidator()
	{
		if (typeof(TRequest).IsAssignableTo(typeof(IRequireAuthorization)))
		{
			Include(new AuthorizableValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IRequireActivityAuthorization)))
		{
			Include(new AuthorizableForResourceValidator<TRequest>());
		}
	}
}
