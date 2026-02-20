// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Extension methods for configuring message validation in the dispatch builder. Provides methods to register validation services and
/// configure custom validators.
/// </summary>
public static class ValidationDispatchBuilderExtensions
{
	/// <summary>
	/// Adds dispatch validation services to the builder. Registers default validation infrastructure including validator resolvers and
	/// validation middleware for message processing.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder AddDispatchValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchValidation();

		return builder;
	}

	/// <summary>
	/// Configures custom message validators for the dispatch system. Allows registration of domain-specific validators for message types
	/// through the provided service collection configuration action.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="registerValidators"> Action to register custom validators with the service collection. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder" /> or <paramref name="registerValidators" /> is null.
	/// </exception>
	public static IDispatchBuilder WithCustomValidators(this IDispatchBuilder builder, Action<IServiceCollection> registerValidators)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(registerValidators);

		registerValidators(builder.Services);
		return builder;
	}
}
