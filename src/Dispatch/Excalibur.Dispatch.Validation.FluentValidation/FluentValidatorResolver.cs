// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.FluentValidation;

/// <summary>
/// Resolves and executes FluentValidation validators for dispatch messages.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses the non-generic IValidator interface to avoid reflection
/// in message type resolution, though it still requires generic type creation at runtime.
/// For full AOT support, use <see cref="AotFluentValidatorResolver"/> with source-generated validators.
/// </para>
/// <para>
/// The generic IValidator{T} type resolution is cached per message type to avoid
/// repeated MakeGenericType calls on the hot path.
/// </para>
/// </remarks>
/// <param name="provider">The service provider used to resolve validators.</param>
public sealed class FluentValidatorResolver(IServiceProvider provider) : IValidatorResolver
{
	/// <summary>
	/// Cache of generic IValidator{T} types per message type to avoid MakeGenericType per call.
	/// </summary>
	private static readonly ConcurrentDictionary<Type, Type> ValidatorTypeCache = new();

	/// <summary>
	/// Attempts to validate the given message using registered FluentValidation validators.
	/// </summary>
	/// <param name="message">The message to validate.</param>
	/// <returns>A validation result if validators exist; otherwise, null.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2060:Call to 'System.Type.MakeGenericType(params Type[])' can not be statically analyzed",
		Justification = "FluentValidation requires runtime generic type creation. Message types are preserved via source generation.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "FluentValidation requires dynamic code. Consider migrating to source-generated validators for full AOT support.")]
	public IValidationResult? TryValidate(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		var messageType = message.GetType();

		// Get the generic IValidator<T> type for this message type (cached to avoid MakeGenericType per call)
		var validatorType = ValidatorTypeCache.GetOrAdd(messageType,
			static type => typeof(IValidator<>).MakeGenericType(type));

		// Get all validators for this message type using the service provider
		var validators = provider.GetServices(validatorType).Cast<IValidator>().ToArray();

		if (validators.Length == 0)
		{
			return null;
		}

		var failures = new List<ValidationFailure>();
		var context = new ValidationContext<IDispatchMessage>(message);

		foreach (var validator in validators)
		{
			// Use the non-generic IValidator.Validate method
			var result = validator.Validate(context);
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
}
