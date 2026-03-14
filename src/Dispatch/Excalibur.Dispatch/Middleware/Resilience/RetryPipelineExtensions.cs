// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Extension methods for adding retry middleware to the dispatch pipeline.
/// </summary>
public static class RetryPipelineExtensions
{
	/// <summary>
	/// Adds retry middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The retry middleware automatically retries failed downstream handlers using
	/// configurable retry policies (exponential backoff, fixed delay, etc.).
	/// </para>
	/// <para>
	/// Retry services must be registered separately in the DI container. This method
	/// only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseExceptionMapping()
	///        .UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseRetry()            // Retry before circuit breaker
	///        .UseCircuitBreaker();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseRetry(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<RetryMiddleware>();
	}
}
