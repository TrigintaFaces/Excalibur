// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.ZeroAlloc;

/// <summary>
/// Extension methods for adding zero-allocation validation middleware to the dispatch pipeline.
/// </summary>
public static class ZeroAllocPipelineExtensions
{
	/// <summary>
	/// Adds zero-allocation validation middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The zero-allocation validation middleware performs message validation using
	/// stack-allocated buffers and zero-copy techniques, minimizing GC pressure
	/// in high-throughput scenarios.
	/// </para>
	/// <para>
	/// This method adds the middleware directly to the pipeline using
	/// <see cref="ZeroAllocationValidationMiddleware"/>. For service registration
	/// configuration, use <see cref="ZeroAllocConfigurationExtensions.UseZeroAllocation"/> instead.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseZeroAllocMiddleware()  // Validate with minimal allocations
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseZeroAllocMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<ZeroAllocationValidationMiddleware>();
	}
}
