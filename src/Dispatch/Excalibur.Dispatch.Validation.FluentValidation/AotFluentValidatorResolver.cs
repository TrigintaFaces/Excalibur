// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation;

/// <summary>
/// AOT-compatible FluentValidation validator resolver that uses compile-time code generation.
/// </summary>
/// <remarks>
/// This implementation avoids reflection by using source-generated dispatch logic for different message types,
/// making it suitable for Native AOT scenarios.
/// </remarks>
/// <param name="provider">The service provider for dependency injection.</param>
public sealed class AotFluentValidatorResolver(IServiceProvider provider) : IValidatorResolver
{
	private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));

	/// <summary>
	/// Attempts to validate a message using AOT-compatible validation logic.
	/// </summary>
	/// <param name="message">The message to validate.</param>
	/// <returns>Validation result if validators exist, null otherwise.</returns>
	public IValidationResult? TryValidate(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		// This method will be enhanced with source-generated switch statements
		// that dispatch to strongly-typed validation methods for known message types
		return ValidateMessage(message);
	}

	/// <summary>
	/// Generic validation method for strongly-typed messages.
	/// </summary>
	/// <typeparam name="TMessage">The type of message to validate.</typeparam>
	/// <param name="message">The message to validate.</param>
	/// <returns>Validation result if validators exist, null otherwise.</returns>
	internal IValidationResult? ValidateTyped<TMessage>(TMessage message)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(message);

		var validators = _provider.GetServices<IValidator<TMessage>>().ToArray();
		if (validators.Length == 0)
		{
			return null;
		}

		var failures = new List<ValidationFailure>();

		foreach (var validator in validators)
		{
			var result = validator.Validate(message);
			if (!result.IsValid)
			{
				failures.AddRange(result.Errors);
			}
		}

		if (failures.Count == 0)
		{
			return SerializableValidationResult.Success();
		}

		var errors = failures
			.Select(failure => (object)new ValidationError(failure.PropertyName, failure.ErrorMessage) { ErrorCode = failure.ErrorCode })
			.ToArray();

		return SerializableValidationResult.Failed(errors);
	}

	/// <summary>
	/// Validates a message using type-specific validation logic.
	/// </summary>
	/// <param name="message">The message to validate.</param>
	/// <returns>Never returns; always throws.</returns>
	/// <exception cref="NotSupportedException">
	/// Always thrown because AOT FluentValidation requires source-generated dispatch logic.
	/// </exception>
	/// <remarks>
	/// Until the source generator provides type-specific validation dispatch, this method
	/// throws to make the gap visible rather than silently returning null.
	/// Use <see cref="FluentValidatorResolver"/> for reflection-based validation.
	/// </remarks>
	private static IValidationResult? ValidateMessage(IDispatchMessage message) =>
		throw new NotSupportedException(
			"AOT FluentValidation requires source generator. Use FluentValidatorResolver for reflection-based validation.");
}
