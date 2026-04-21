// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;

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
	private readonly Lazy<IAotValidationDispatcher?> _dispatcher = new(
		() => provider.GetService<IAotValidationDispatcher>(),
		LazyThreadSafetyMode.PublicationOnly);

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

		var dispatcher = _dispatcher.Value
			?? throw new InvalidOperationException(
				"AOT validation requires the Excalibur.Dispatch.SourceGenerators package. " +
				"Add it as an Analyzer reference.");

		return dispatcher.TryValidate(message, _provider);
	}
}
