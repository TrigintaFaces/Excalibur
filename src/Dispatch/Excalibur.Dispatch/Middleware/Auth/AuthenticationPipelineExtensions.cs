// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Auth;

/// <summary>
/// Extension methods for adding authentication middleware to the dispatch pipeline.
/// </summary>
public static class AuthenticationPipelineExtensions
{
	/// <summary>
	/// Adds authentication middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The authentication middleware establishes the identity of the caller by validating
	/// credentials attached to the message context before passing it to downstream handlers.
	/// </para>
	/// <para>
	/// Authentication services must be registered separately in the DI container. This method
	/// only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseExceptionMapping()
	///        .UseAuthentication()   // Authenticate first
	///        .UseAuthorization()    // Then authorize
	///        .UseValidation()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseAuthentication(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<AuthenticationMiddleware>();
	}
}
