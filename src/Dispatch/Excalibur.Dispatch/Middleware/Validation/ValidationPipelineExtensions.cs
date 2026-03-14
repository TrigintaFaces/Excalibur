// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Validation;

/// <summary>
/// Extension methods for adding validation middleware to the dispatch pipeline.
/// </summary>
public static class ValidationPipelineExtensions
{
	/// <summary>
	/// Adds validation middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The validation middleware runs registered <c>IValidator&lt;T&gt;</c> implementations
	/// against the incoming message before passing it to downstream handlers.
	/// If validation fails, a <see cref="Abstractions.Exceptions.ValidationException"/> is thrown.
	/// </para>
	/// <para>
	/// Validators must be registered separately in the DI container. This method only
	/// adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseExceptionMapping()
	///        .UseValidation()      // Validate early, before authorization
	///        .UseAuthorization()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseValidation(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<ValidationMiddleware>();
	}
}
