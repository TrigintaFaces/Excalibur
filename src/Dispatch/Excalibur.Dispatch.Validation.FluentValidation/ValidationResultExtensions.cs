// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

using FluentValidation;

using ValidationResult = FluentValidation.Results.ValidationResult;

namespace Excalibur.Dispatch.Validation.FluentValidation;

/// <summary>
/// Extension methods for working with FluentValidation in the Dispatch framework.
/// </summary>
public static class ValidationResultExtensions
{
	/// <summary>
	/// Validates the message using a FluentValidation validator and converts the result to a Dispatch-compatible ValidationResult.
	/// </summary>
	/// <typeparam name="TMessage">The message type.</typeparam>
	/// <typeparam name="TValidator">The FluentValidation validator type.</typeparam>
	/// <param name="message">The message instance to validate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A Dispatch-compatible SerializableValidationResult.</returns>
	public static async Task<SerializableValidationResult> ValidateWithAsync<TMessage, TValidator>(
		this TMessage message,
		CancellationToken cancellationToken)
		where TMessage : IDispatchMessage
		where TValidator : AbstractValidator<TMessage>, new()
	{
		var validator = new TValidator();
		var context = new ValidationContext<TMessage>(message);

		var fluentResult = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
		return (SerializableValidationResult)fluentResult.ToDispatchResult();
	}

	/// <summary>
	/// Validates the message using a FluentValidation validator and converts the result to a Dispatch-compatible ValidationResult.
	/// </summary>
	/// <typeparam name="TMessage">The message type.</typeparam>
	/// <typeparam name="TValidator">The FluentValidation validator type.</typeparam>
	/// <param name="message">The message instance to validate.</param>
	/// <returns>A Dispatch-compatible IValidationResult.</returns>
	public static IValidationResult ValidateWith<TMessage, TValidator>(this TMessage message)
		where TMessage : IDispatchMessage
		where TValidator : AbstractValidator<TMessage>, new()
	{
		var validator = new TValidator();

		return validator.Validate(message).ToDispatchResult();
	}

	/// <summary>
	/// Converts a FluentValidation result to a Dispatch-compatible validation result.
	/// </summary>
	/// <param name="fluentResult">The FluentValidation result to convert.</param>
	/// <returns>A Dispatch-compatible validation result.</returns>
	public static IValidationResult ToDispatchResult(this ValidationResult fluentResult)
	{
		ArgumentNullException.ThrowIfNull(fluentResult);

		if (fluentResult.IsValid)
		{
			return SerializableValidationResult.Success();
		}

		var errors = fluentResult.Errors
			.Select(failure => (object)new ValidationError(failure.PropertyName, failure.ErrorMessage) { ErrorCode = failure.ErrorCode })
			.ToArray();

		return SerializableValidationResult.Failed(errors);
	}

	/// <summary>
	/// Converts a FluentValidation result to a Dispatch-compatible validation result.
	/// </summary>
	/// <param name="fluentResult">The FluentValidation result to convert.</param>
	/// <returns>A Dispatch-compatible validation result.</returns>
	/// <remarks>Alias for <see cref="ToDispatchResult"/> for backward compatibility.</remarks>
	public static IValidationResult ToExcaliburResult(this ValidationResult fluentResult) =>
		ToDispatchResult(fluentResult);
}
