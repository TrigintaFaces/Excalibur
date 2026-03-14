// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Middleware.Batch;

/// <summary>
/// Extension methods for adding batching middleware to the dispatch pipeline.
/// </summary>
public static class BatchingPipelineExtensions
{
	/// <summary>
	/// Adds message batching middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The batching middleware groups multiple messages together and dispatches them
	/// as a batch, improving throughput for high-volume scenarios.
	/// </para>
	/// <para>
	/// Batching options must be configured separately via <c>IOptions&lt;BatchOptions&gt;</c>.
	/// This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseBatching()             // Batch after validation
	///        .UseTransaction();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseBatching(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<UnifiedBatchingMiddleware>();
	}
}
