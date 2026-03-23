// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.ErrorHandling;
using Excalibur.Dispatch.Middleware.Logging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Middleware.Validation;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Extension methods for adding middleware presets and fine-grained stacks to the dispatch pipeline.
/// </summary>
public static class MiddlewarePresetExtensions
{
	/// <summary>
	/// Adds the security middleware stack: authentication, authorization, and tenant identity.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Composes the following middleware in order:
	/// <list type="number">
	///   <item><description>AuthenticationMiddleware -- validates caller identity</description></item>
	///   <item><description>AuthorizationMiddleware -- enforces permissions</description></item>
	///   <item><description>TenantIdentityMiddleware -- resolves tenant context</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseSecurityStack(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.UseAuthentication();
		_ = builder.UseAuthorization();
		return builder.UseTenantIdentity();
	}

	/// <summary>
	/// Adds the resilience middleware stack: timeout, retry, and circuit breaker.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Composes the following middleware in order:
	/// <list type="number">
	///   <item><description>TimeoutMiddleware -- enforces timeout (outermost)</description></item>
	///   <item><description>RetryMiddleware -- retries transient failures</description></item>
	///   <item><description>CircuitBreakerMiddleware -- protects downstream from cascading failure</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseResilienceStack(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.UseTimeout();
		_ = builder.UseRetry();
		return builder.UseCircuitBreaker();
	}

	/// <summary>
	/// Adds the validation middleware stack: validation and exception mapping.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// Composes the following middleware in order:
	/// <list type="number">
	///   <item><description>ValidationMiddleware -- validates message payloads</description></item>
	///   <item><description>ExceptionMappingMiddleware -- maps exceptions to structured results</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseValidationStack(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ValidationMiddleware>();
		_ = builder.UseMiddleware<ValidationMiddleware>();
		return builder.UseExceptionMapping();
	}

	/// <summary>
	/// Applies sensible default middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This method applies the recommended default middleware stack for most applications:
	/// <list type="number">
	///   <item><description>ValidationMiddleware -- validates message payloads (stage: Validation)</description></item>
	///   <item><description>LoggingMiddleware -- audit logging (stage: Logging)</description></item>
	///   <item><description>TimeoutMiddleware -- pipeline-level timeout (stage: PreProcessing)</description></item>
	///   <item><description>RetryMiddleware -- retries transient failures (stage: ErrorHandling)</description></item>
	///   <item><description>ExceptionMappingMiddleware -- maps exceptions to structured results (stage: PostProcessing)</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// For additional security middleware, chain <see cref="UseSecurityStack"/>.
	/// For observability (metrics/tracing), use <c>AddDispatchObservability()</c> from the Observability package.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder WithDefaults(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.UseValidation();
		_ = builder.UseLogging();
		_ = builder.UseTimeout();
		_ = builder.UseRetry();
		return builder.UseExceptionMapping();
	}

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
	/// Adds production middleware preset: retry and exception mapping.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This preset provides core resilience and error handling for production scenarios.
	/// </para>
	/// <para>
	/// Middleware included:
	/// <list type="bullet">
	///   <item><description>RetryMiddleware</description></item>
	///   <item><description>ExceptionMappingMiddleware</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// For full production observability (metrics and tracing), pair with
	/// <c>AddDispatchObservability()</c> from the Excalibur.Dispatch.Observability package.
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
	/// Adds full middleware preset: logging, validation, retry, exception mapping.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <remarks>
	/// <para>
	/// This preset includes core middleware with sensible defaults.
	/// Use when you want comprehensive error handling and resilience.
	/// </para>
	/// <para>
	/// Middleware included (in pipeline order):
	/// <list type="bullet">
	///   <item><description>LoggingMiddleware (Information level, completion only)</description></item>
	///   <item><description>ValidationMiddleware</description></item>
	///   <item><description>RetryMiddleware</description></item>
	///   <item><description>ExceptionMappingMiddleware</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// For full observability (metrics and tracing), pair with
	/// <c>AddDispatchObservability()</c> from the Excalibur.Dispatch.Observability package.
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
