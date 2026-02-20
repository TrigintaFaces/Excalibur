// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Http;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Observability.Propagation;
using Excalibur.Dispatch.Observability.Sampling;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring observability via <see cref="IDispatchBuilder"/>.
/// </summary>
public static class DispatchBuilderObservabilityExtensions
{
	/// <summary>
	/// Adds Dispatch observability (context tracking, metrics, tracing) via the builder.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Optional action to configure observability options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddObservability(obs => obs.Enabled = true);
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder AddObservability(
		this IDispatchBuilder builder,
		Action<ContextObservabilityOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchObservability(configure);
		return builder;
	}

	/// <summary>
	/// Adds Dispatch observability using <see cref="IConfiguration"/>.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configuration">The configuration section for observability options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configuration"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddObservability(configuration);
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types")]
	[RequiresDynamicCode("Configuration binding may require dynamic code generation")]
	public static IDispatchBuilder AddObservability(
		this IDispatchBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddDispatchObservability(configuration);
		return builder;
	}

	/// <summary>
	/// Adds distributed tracing middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This middleware creates OpenTelemetry spans for each message processed.
	/// To export traces, add the Dispatch activity source to your OpenTelemetry configuration:
	/// </para>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithTracing(tracing => tracing.AddSource("Excalibur.Dispatch"));
	/// </code>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseTracing();
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseTracing(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<TracingMiddleware>();
		return builder.UseMiddleware<TracingMiddleware>();
	}

	/// <summary>
	/// Adds metrics collection middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This middleware records metrics for message processing including:
	/// <list type="bullet">
	/// <item><c>dispatch.messages.processed</c> - Counter of processed messages</item>
	/// <item><c>dispatch.messages.duration</c> - Histogram of processing duration</item>
	/// <item><c>dispatch.messages.failed</c> - Counter of failed messages</item>
	/// </list>
	/// </para>
	/// <para>
	/// To export metrics, add the Excalibur meter to your OpenTelemetry configuration:
	/// </para>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithMetrics(metrics => metrics.AddDispatchMetrics());
	/// </code>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseMetrics();
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseMetrics(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IDispatchMetrics, DispatchMetrics>();
		builder.Services.TryAddSingleton<MetricsMiddleware>();
		return builder.UseMiddleware<MetricsMiddleware>();
	}

	/// <summary>
	/// Adds both distributed tracing and metrics middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// This is a convenience method equivalent to calling both
	/// <see cref="UseTracing"/> and <see cref="UseMetrics"/>.
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.UseOpenTelemetry();
	/// });
	/// </code>
	/// </example>
	public static IDispatchBuilder UseOpenTelemetry(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseTracing().UseMetrics();
	}

	/// <summary>
	/// Adds W3C Trace Context propagation middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This middleware extracts W3C <c>traceparent</c> and <c>tracestate</c> headers
	/// from the message context and creates a child activity linked to the parent trace.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder UseW3CTraceContext(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<W3CTraceContextMiddleware>();
		return builder.UseMiddleware<W3CTraceContextMiddleware>();
	}

	/// <summary>
	/// Adds trace sampling middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Optional action to configure trace sampler options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	public static IDispatchBuilder UseTraceSampling(
		this IDispatchBuilder builder,
		Action<TraceSamplerOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var optionsBuilder = builder.Services.AddOptions<TraceSamplerOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.TryAddSingleton<ITraceSampler, TraceSampler>();
		builder.Services.TryAddSingleton<TraceSamplerMiddleware>();
		return builder.UseMiddleware<TraceSamplerMiddleware>();
	}

	/// <summary>
	/// Adds W3C trace context propagation to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddW3CTracingPropagator(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ITracingContextPropagator, W3CTracingContextPropagator>();
		return services;
	}

	/// <summary>
	/// Adds B3 trace context propagation to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddB3TracingPropagator(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ITracingContextPropagator, B3TracingContextPropagator>();
		return services;
	}
}
