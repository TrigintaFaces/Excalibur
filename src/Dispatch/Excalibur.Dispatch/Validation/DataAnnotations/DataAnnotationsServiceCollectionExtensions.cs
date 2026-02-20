// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;
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

		return builder;
	}
}
