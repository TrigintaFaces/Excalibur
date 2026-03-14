// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Timeout;

/// <summary>
/// Extension methods for adding timeout middleware to the dispatch pipeline.
/// </summary>
public static class TimeoutPipelineExtensions
{
	/// <summary>
	/// Adds timeout middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The timeout middleware enforces a maximum execution duration for downstream handlers.
	/// If the handler does not complete within the configured timeout, an
	/// <see cref="OperationCanceledException"/> is thrown.
	/// </para>
	/// <para>
	/// Timeout services must be registered separately in the DI container. This method
	/// only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseExceptionMapping()
	///        .UseTimeout()          // Enforce timeout early
	///        .UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseTimeout(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<TimeoutMiddleware>();
	}
}
