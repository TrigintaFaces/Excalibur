// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

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
/// <para>
/// This implementation avoids reflection by delegating to a source-generated
/// <see cref="IAotValidationDispatcher"/> that maps each message type to its
/// strongly-typed validators at compile time.
/// </para>
/// <para>
/// If no <see cref="IAotValidationDispatcher"/> is registered in the service provider,
/// <see cref="TryValidate"/> throws <see cref="InvalidOperationException"/> directing
/// the consumer to add the <c>Excalibur.Dispatch.SourceGenerators</c> analyzer package.
/// </para>
/// </remarks>
/// <param name="provider">The service provider for dependency injection.</param>
public sealed class AotFluentValidatorResolver(IServiceProvider provider) : IValidatorResolver
{
	private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
	private readonly ConcurrentDictionary<Type, object> _validatorCache = new();
	private IAotValidationDispatcher? _dispatcher;
	private volatile bool _dispatcherResolved;

	/// <summary>
	/// Validates a message using AOT-compatible, source-generated dispatch logic.
	/// </summary>
	/// <param name="message">The message to validate.</param>
	/// <returns>Validation result if validators exist, null otherwise.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when no <see cref="IAotValidationDispatcher"/> is registered.
	/// This means the <c>Excalibur.Dispatch.SourceGenerators</c> package is not referenced
	/// as an analyzer, or no FluentValidation validators for <c>IDispatchMessage</c> types
	/// were discovered at compile time.
	/// </exception>
	public IValidationResult? TryValidate(IDispatchMessage message)
	{
		ArgumentNullException.ThrowIfNull(message);

		if (!_dispatcherResolved)
		{
			_dispatcher = _provider.GetService<IAotValidationDispatcher>();
			_dispatcherResolved = true;
		}

		if (_dispatcher is null)
		{
			throw new InvalidOperationException(
				"AOT validation requires the Excalibur.Dispatch.SourceGenerators package. " +
				"Add it as an Analyzer reference.");
		}

		return _dispatcher.TryValidate(message, _provider);
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

		var validators = GetOrCacheValidators<TMessage>();
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
	/// Gets or creates a cached array of validators for the specified message type.
	/// First call per type resolves from DI; subsequent calls return the cached array.
	/// </summary>
	private IValidator<TMessage>[] GetOrCacheValidators<TMessage>()
		where TMessage : IDispatchMessage
	{
		if (_validatorCache.TryGetValue(typeof(TMessage), out var cached))
		{
			return (IValidator<TMessage>[])cached;
		}

		var validators = _provider.GetServices<IValidator<TMessage>>().ToArray();
		_validatorCache.TryAdd(typeof(TMessage), validators);
		return validators;
	}
}
