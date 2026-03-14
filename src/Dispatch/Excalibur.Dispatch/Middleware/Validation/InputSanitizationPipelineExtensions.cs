// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Validation;

/// <summary>
/// Extension methods for adding input sanitization middleware to the dispatch pipeline.
/// </summary>
public static class InputSanitizationPipelineExtensions
{
	/// <summary>
	/// Adds input sanitization middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The input sanitization middleware sanitizes message properties to prevent
	/// injection attacks (XSS, SQL injection, etc.) before they reach handlers.
	/// </para>
	/// <para>
	/// Sanitization rules are configured via <c>IOptions&lt;InputSanitizationOptions&gt;</c>.
	/// This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseInputSanitization()   // Sanitize before validation
	///        .UseValidation()
	///        .UseTransaction();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseInputSanitization(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<InputSanitizationMiddleware>();
	}
}
