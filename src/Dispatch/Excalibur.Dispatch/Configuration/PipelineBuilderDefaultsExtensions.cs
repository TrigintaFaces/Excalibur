// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware.ErrorHandling;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Validation;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Extension methods for applying default middleware to a pipeline builder.
/// </summary>
public static class PipelineBuilderDefaultsExtensions
{
	/// <summary>
	/// Adds sensible default middleware to the pipeline in the recommended order:
	/// validation, logging, timeout, retry, and exception mapping.
	/// </summary>
	/// <param name="builder">The pipeline builder.</param>
	/// <returns>The pipeline builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method provides a production-ready middleware stack with a single call.
	/// You can add additional middleware before or after this call:
	/// </para>
	/// <code>
	/// dispatch.ConfigurePipeline("default", pipeline =>
	/// {
	///     pipeline.UseDefaults();
	///     pipeline.Use&lt;CustomMetricsMiddleware&gt;();
	/// });
	/// </code>
	/// <para>
	/// The default middleware types must be registered in DI (e.g., via <c>AddDispatchValidation()</c>).
	/// Middleware that is not registered in DI will throw at pipeline build time.
	/// </para>
	/// </remarks>
	public static IPipelineBuilder UseDefaults(this IPipelineBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder
			.Use<ValidationMiddleware>()
			.Use<LoggingMiddleware>()
			.Use<TimeoutMiddleware>()
			.Use<RetryMiddleware>()
			.Use<ExceptionMappingMiddleware>();

		return builder;
	}
}
