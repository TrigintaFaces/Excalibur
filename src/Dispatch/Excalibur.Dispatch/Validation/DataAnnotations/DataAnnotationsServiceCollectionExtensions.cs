// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Validation;
using Excalibur.Dispatch.Validation.DataAnnotations;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring DataAnnotations-based validation.
/// </summary>
public static class DataAnnotationsServiceCollectionExtensions
{
	/// <summary>
	/// Adds DataAnnotations-based validation to the dispatch pipeline.
	/// Zero external dependencies - uses only System.ComponentModel.DataAnnotations.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder WithDataAnnotationsValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Replace the default NoOpValidatorResolver with DataAnnotationsValidatorResolver
		_ = builder.Services.RemoveAll<IValidatorResolver>();
		_ = builder.Services.AddSingleton<IValidatorResolver, DataAnnotationsValidatorResolver>();

		// Hand attribute evaluation to the resolver alone. The middleware evaluates DataAnnotations itself
		// by default, and since every source accumulates rather than short-circuits, leaving both on
		// reports each attribute violation twice. Configured rather than hard-set, so a caller who opts
		// back in afterwards still wins.
		// Fully qualified: the enclosing Excalibur.Dispatch.Validation namespace declares its own unrelated
		// ValidationOptions, and an unqualified reference silently binds to that one instead.
		_ = builder.Services.Configure<global::Excalibur.Dispatch.Options.Middleware.ValidationOptions>(
			options => options.UseDataAnnotations = false);

		return builder;
	}
}
