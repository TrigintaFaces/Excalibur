// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Extension methods for adding performance middleware to the dispatch pipeline.
/// </summary>
public static class PerformancePipelineExtensions
{
	/// <summary>
	/// Adds performance monitoring middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The performance middleware tracks execution timing, throughput, and latency
	/// metrics for dispatched messages, enabling performance monitoring and alerting.
	/// </para>
	/// <para>
	/// Performance services must be registered separately in the DI container. This method
	/// only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UsePerformance()         // Measure early for accurate timing
	///        .UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UsePerformance(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<PerformanceMiddleware>();
	}
}
