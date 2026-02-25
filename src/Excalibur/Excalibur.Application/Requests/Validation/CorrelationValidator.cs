// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Application.Requests.Validation;

/// <summary>
/// Validator for <see cref="IAmCorrelatable" /> properties. Ensures the correlation ID is valid.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
public sealed class CorrelationValidator<TRequest> : RulesFor<TRequest, IAmCorrelatable>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CorrelationValidator{TRequest}" /> class.
	/// </summary>
	public CorrelationValidator() => _ = RuleFor(static x => x.CorrelationId).IsValidCorrelationId();
}
