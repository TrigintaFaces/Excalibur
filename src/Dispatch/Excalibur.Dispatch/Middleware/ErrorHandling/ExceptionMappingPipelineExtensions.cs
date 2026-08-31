// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.ErrorHandling;

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
	/// <see cref="Excalibur.Dispatch.IExceptionMapper"/> service.
	/// </para>
	/// <para>
	/// This method automatically registers the <see cref="Excalibur.Dispatch.IExceptionMapper"/>
	/// service with default configuration if not already registered. The defaults include:
	/// <list type="bullet">
	///   <item><description>Automatic mapping of <see cref="Excalibur.Dispatch.ApiException"/> hierarchy using ToProblemDetails()</description></item>
	///   <item><description>Default mapper returns 500 Internal Server Error for unmapped exceptions</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// For custom exception mappings, use <see cref="Excalibur.Dispatch.Configuration.ExceptionMappingDispatchBuilderExtensions.WithExceptionMapping"/>
	/// before calling this method.
	/// </para>
	/// <para>
	/// The order you call this in does not decide where the middleware runs. Position comes from the stage:
	/// this middleware is post-processing, retry and the circuit breaker are error-handling, and the pipeline
	/// composes the lower stage as the outer wrapper. Exception mapping therefore always runs outside both,
	/// whichever order you register them in. Call order decides position only among middleware sharing a
	/// stage, where it is the registration order that applies.
	/// </para>
	/// <para>
	/// Every exception a handler throws reaches this middleware. Retry runs below it and decides only how
	/// many attempts a fault gets: a fault it judges permanent is abandoned after one attempt, a transient
	/// one after the configured attempts, and in both cases the original exception is left to propagate here
	/// with its type and message intact for your mapper to match on.
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
