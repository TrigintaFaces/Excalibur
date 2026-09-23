// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
	/// Batching is configured through <see cref="UnifiedBatchingOptions"/>, set the usual way —
	/// <c>services.Configure&lt;UnifiedBatchingOptions&gt;(...)</c> or a bound configuration section.
	/// Calling this method also registers validation for those options, so a non-positive batch size,
	/// delay, or degree of parallelism fails at host start rather than degrading silently once messages
	/// begin to flow.
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

		// A validator, so the ValidateOnStart below is not a no-op. TryAddEnumerable keeps this
		// idempotent across repeated UseBatching calls rather than stacking duplicate validators.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<UnifiedBatchingOptions>, UnifiedBatchingOptionsValidator>());

		_ = builder.Services.AddOptions<UnifiedBatchingOptions>().ValidateOnStart();

		return builder.UseMiddleware<UnifiedBatchingMiddleware>();
	}
}
