// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using FluentValidation;

namespace Excalibur.Application.Requests.Validation;

/// <summary>
/// Base class for validating requests. Dynamically includes validators based on implemented interfaces.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public abstract class RequestValidator<TRequest> : AbstractValidator<TRequest>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RequestValidator{TRequest}" /> class. Includes validators for supported interfaces such
	/// as <see cref="IAmMultiTenant" />, <see cref="IAmCorrelatable" />, and <see cref="IActivity" />.
	/// </summary>
	protected RequestValidator()
	{
		if (typeof(TRequest).IsAssignableTo(typeof(IAmMultiTenant)))
		{
			Include(new MultiTenantValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IAmCorrelatable)))
		{
			Include(new CorrelationValidator<TRequest>());
		}

		if (typeof(TRequest).IsAssignableTo(typeof(IActivity)))
		{
			Include(new ActivityValidator<TRequest>());
		}
	}
}
