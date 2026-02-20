// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using FluentValidation;
using FluentValidation.Results;

namespace Excalibur.Application.Requests.Validation;

/// <summary>
/// Base class for creating validators that focus on specific parts of a request.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being validated. </typeparam>
/// <typeparam name="TPart"> The type of the part of the request being validated. </typeparam>
public abstract class RulesFor<TRequest, TPart> : AbstractValidator<TPart>, IValidator<TRequest>
{
	/// <inheritdoc />
	public ValidationResult Validate(TRequest instance) =>
		Validate((TPart)(object)instance! ?? throw new ArgumentNullException(nameof(instance)));

	/// <inheritdoc />
	public Task<ValidationResult> ValidateAsync(TRequest instance, CancellationToken cancellation) =>
		ValidateAsync((TPart)(object)instance! ?? throw new ArgumentNullException(nameof(instance)), cancellation);
}
