// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Validation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Validation.Context;

/// <summary>
/// Extension methods for IDispatchBuilder to add context validation.
/// </summary>
public static class ContextValidationDispatchBuilderExtensions
{
	/// <summary>
	/// Adds context validation to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	[RequiresDynamicCode("Uses dynamic code generation which requires JIT compilation")]
	public static IDispatchBuilder AddContextValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddContextValidation();

		return builder;
	}

	/// <summary>
	/// Adds context validation with configuration to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configureOptions"> Action to configure options. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddContextValidation(
		this IDispatchBuilder builder,
		Action<ContextValidationOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = builder.Services.AddContextValidation(configureOptions);

		return builder;
	}

	/// <summary>
	/// Uses strict context validation in the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder UseStrictContextValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.UseStrictContextValidation();

		return builder;
	}

	/// <summary>
	/// Uses lenient context validation in the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder UseLenientContextValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.UseLenientContextValidation();

		return builder;
	}
}
