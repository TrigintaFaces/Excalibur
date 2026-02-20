// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring FluentValidation with the Excalibur framework.
/// </summary>
public static class FluentValidationServiceCollectionExtensions
{
	/// <summary>
	/// Adds FluentValidation-based validation to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for chaining.</returns>
	/// <remarks>
	/// This method registers <see cref="FluentValidatorResolver"/> as the <see cref="IValidatorResolver"/>
	/// implementation. Ensure your FluentValidation validators are registered with the service collection.
	/// </remarks>
	public static IDispatchBuilder WithFluentValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSingleton<IValidatorResolver, FluentValidatorResolver>();
		return builder;
	}

	/// <summary>
	/// Adds AOT-compatible FluentValidation to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The dispatch builder for chaining.</returns>
	/// <remarks>
	/// This method registers <see cref="AotFluentValidatorResolver"/> which is designed for
	/// Native AOT scenarios. Requires source-generated validator dispatch for full AOT support.
	/// </remarks>
	public static IDispatchBuilder WithAotFluentValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddSingleton<IValidatorResolver, AotFluentValidatorResolver>();
		return builder;
	}
}
