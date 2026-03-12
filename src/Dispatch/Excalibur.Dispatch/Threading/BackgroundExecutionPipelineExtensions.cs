// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Extension methods for adding background execution middleware to the dispatch pipeline.
/// </summary>
public static class BackgroundExecutionPipelineExtensions
{
	/// <summary>
	/// Adds background execution middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The background execution middleware offloads handler execution to a background
	/// thread, allowing the caller to return immediately while processing continues
	/// asynchronously.
	/// </para>
	/// <para>
	/// Background execution services must be registered separately in the DI container.
	/// This method only adds the middleware to the pipeline.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.UseAuthentication()
	///        .UseAuthorization()
	///        .UseValidation()
	///        .UseBackgroundExecution()  // Offload after validation
	///        .UseRetry();
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseBackgroundExecution(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.UseMiddleware<BackgroundExecutionMiddleware>();
	}
}
