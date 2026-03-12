// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Extension methods for adding rate limiting middleware to the dispatch pipeline.
/// </summary>
public static class RateLimitingPipelineExtensions
{
	/// <summary>
	/// Adds rate limiting middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The rate limiting middleware enforces configurable rate limits to protect the system
	/// from excessive message processing and prevent resource exhaustion. It supports
	/// per-message-type, per-tenant, and global rate limiting with multiple algorithms
	/// (token bucket, sliding window, fixed window, concurrency).
	/// </para>
	/// <para>
	/// Rate limiting options must be configured separately via
	/// <c>services.Configure&lt;RateLimitingOptions&gt;()</c>. This method only adds the
	/// middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseExceptionMapping()
	///        .UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseRateLimiting()   // Before retry to prevent retry amplification
	///        .UseRetry()
	///        .UseCircuitBreaker();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseRateLimiting(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<RateLimitingMiddleware>();
	}
}
