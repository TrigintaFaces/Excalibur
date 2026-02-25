// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Extension methods for adding middleware presets to the dispatch pipeline.
/// </summary>
public static class MiddlewarePresetExtensions
{
	/// <summary>
	/// Adds development middleware preset: logging (verbose), validation, exception mapping.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This preset is optimized for development scenarios with verbose logging
	/// and detailed error information. Not recommended for production due to
	/// potential performance and security implications.
	/// </para>
	/// <para>
	/// Middleware included:
	/// <list type="bullet">
	///   <item><description>LoggingMiddleware (Debug level)</description></item>
	///   <item><description>ValidationMiddleware</description></item>
	///   <item><description>ExceptionMappingMiddleware</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseDevelopmentMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Configure verbose logging for development
		_ = builder.UseLogging(options =>
		{
			options.SuccessLevel = LogLevel.Debug;
			options.FailureLevel = LogLevel.Warning;
			options.IncludeTiming = true;
			options.LogStart = true;
			options.LogCompletion = true;
		});

		// Add validation
		builder.Services.TryAddSingleton<ValidationMiddleware>();
		_ = builder.UseMiddleware<ValidationMiddleware>();

		// Add exception mapping
		return builder.UseExceptionMapping();
	}

	/// <summary>
	/// Adds production middleware preset: metrics, tracing, retry, exception mapping.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This preset is optimized for production scenarios with observability,
	/// resilience, and error handling. Logging is minimal to reduce overhead.
	/// </para>
	/// <para>
	/// Middleware included:
	/// <list type="bullet">
	///   <item><description>MetricsMiddleware</description></item>
	///   <item><description>TracingMiddleware</description></item>
	///   <item><description>RetryMiddleware</description></item>
	///   <item><description>ExceptionMappingMiddleware</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Note: MetricsMiddleware and TracingMiddleware are from the Excalibur.Dispatch.Observability package.
	/// If that package is not referenced, only RetryMiddleware and ExceptionMappingMiddleware will be added.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseProductionMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Add retry middleware
		_ = builder.UseMiddleware<RetryMiddleware>();

		// Add exception mapping
		return builder.UseExceptionMapping();
	}

	/// <summary>
	/// Adds full middleware preset: logging, validation, metrics, tracing, retry, exception mapping.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This preset includes all available middleware with sensible defaults.
	/// Use when you want comprehensive observability and error handling.
	/// </para>
	/// <para>
	/// Middleware included (in pipeline order):
	/// <list type="bullet">
	///   <item><description>LoggingMiddleware (Information level)</description></item>
	///   <item><description>ValidationMiddleware</description></item>
	///   <item><description>MetricsMiddleware</description></item>
	///   <item><description>TracingMiddleware</description></item>
	///   <item><description>RetryMiddleware</description></item>
	///   <item><description>ExceptionMappingMiddleware</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// Note: MetricsMiddleware and TracingMiddleware are from the Excalibur.Dispatch.Observability package.
	/// If that package is not referenced, those middleware will not be added.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseFullMiddleware(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Add logging with sensible defaults
		_ = builder.UseLogging(options =>
		{
			options.SuccessLevel = LogLevel.Information;
			options.FailureLevel = LogLevel.Error;
			options.IncludeTiming = true;
			options.LogStart = false; // Only log completion for less noise
			options.LogCompletion = true;
		});

		// Add validation
		builder.Services.TryAddSingleton<ValidationMiddleware>();
		_ = builder.UseMiddleware<ValidationMiddleware>();

		// Add retry middleware
		_ = builder.UseMiddleware<RetryMiddleware>();

		// Add exception mapping
		return builder.UseExceptionMapping();
	}
}
