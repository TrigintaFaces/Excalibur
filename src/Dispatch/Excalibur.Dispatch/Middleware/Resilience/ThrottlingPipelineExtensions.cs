// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Extension methods for adding throttling middleware to the dispatch pipeline.
/// </summary>
public static class ThrottlingPipelineExtensions
{
	/// <summary>
	/// Adds throttling middleware to the dispatch pipeline to protect the system from excessive message processing.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The throttling middleware enforces configurable rate limits to protect the system
	/// from excessive message processing and prevent resource exhaustion. It supports
	/// per-message-type, per-tenant, and global rate limiting with multiple algorithms
	/// (token bucket, sliding window, fixed window, concurrency).
	/// </para>
	/// <para>
	/// For identity-based abuse prevention (per-user, per-API-key, per-IP), see
	/// <c>Excalibur.Dispatch.Security.RateLimitingMiddleware</c> and its <c>UseSecurityRateLimiting()</c> extension.
	/// </para>
	/// <para>
	/// Throttling options must be configured separately via
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
	///        .UseThrottling()   // Before retry to prevent retry amplification
	///        .UseRetry()
	///        .UseCircuitBreaker();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseThrottling(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<ThrottlingMiddleware>();
	}
}
