// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Extension methods for adding exception mapping middleware to the pipeline.
/// </summary>
public static class ExceptionMappingPipelineExtensions
{
	/// <summary>
	/// Adds exception mapping middleware to the dispatch pipeline with default configuration.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The builder for fluent configuration. </returns>
	/// <remarks>
	/// <para>
	/// The exception mapping middleware catches all exceptions from downstream handlers
	/// and converts them to RFC 7807 Problem Details format using the registered
	/// <see cref="Abstractions.Mapping.IExceptionMapper"/> service.
	/// </para>
	/// <para>
	/// This method automatically registers the <see cref="Abstractions.Mapping.IExceptionMapper"/>
	/// service with default configuration if not already registered. The defaults include:
	/// <list type="bullet">
	///   <item><description>Automatic mapping of <see cref="Abstractions.Exceptions.ApiException"/> hierarchy using ToProblemDetails()</description></item>
	///   <item><description>Default mapper returns 500 Internal Server Error for unmapped exceptions</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// For custom exception mappings, use <see cref="Configuration.ExceptionMappingDispatchBuilderExtensions.ConfigureExceptionMapping"/>
	/// before calling this method.
	/// </para>
	/// <para>
	/// Recommended pipeline order:
	/// <code>
	/// builder.ConfigurePipeline("default", pipeline =>
	/// {
	///     pipeline.UseTracing();           // First: set up tracing
	///     pipeline.UseExceptionMapping();  // Second: catch exceptions early
	///     pipeline.UseRetry();             // Third: retry after conversion
	///     pipeline.UseCircuitBreaker();    // Fourth: circuit breaker
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseExceptionMapping(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Auto-register exception mapping with defaults if not already registered
		builder.Services.AddExceptionMapping();

		return builder.UseMiddleware<ExceptionMappingMiddleware>();
	}
}
