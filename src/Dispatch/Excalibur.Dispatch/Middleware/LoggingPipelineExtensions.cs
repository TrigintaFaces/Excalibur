// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Extension methods for adding logging middleware to the pipeline.
/// </summary>
public static class LoggingPipelineExtensions
{
	/// <summary>
	/// Adds logging middleware to the dispatch pipeline with default configuration.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// The logging middleware logs the start and completion of message processing
	/// with configurable log levels, timing information, and optional payload inclusion.
	/// </para>
	/// <para>
	/// Default configuration:
	/// <list type="bullet">
	///   <item><description>SuccessLevel: Information</description></item>
	///   <item><description>FailureLevel: Error</description></item>
	///   <item><description>IncludePayload: false (for security)</description></item>
	///   <item><description>IncludeTiming: true</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseLogging(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<LoggingMiddleware>();
		_ = builder.Services.AddOptions<LoggingMiddlewareOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder.UseMiddleware<LoggingMiddleware>();
	}

	/// <summary>
	/// Adds logging middleware to the dispatch pipeline with custom configuration.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure logging options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Use this overload to customize logging behavior:
	/// <code>
	/// dispatch.UseLogging(options =>
	/// {
	///     options.SuccessLevel = LogLevel.Debug;
	///     options.IncludeTiming = true;
	///     options.ExcludeTypes.Add(typeof(HealthCheckQuery));
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseLogging(
		this IDispatchBuilder builder,
		Action<LoggingMiddlewareOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.TryAddSingleton<LoggingMiddleware>();
		_ = builder.Services.AddOptions<LoggingMiddlewareOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder.UseMiddleware<LoggingMiddleware>();
	}
}
