// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga;
using Excalibur.Saga.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring saga telemetry services.
/// </summary>
public static class SagaTelemetryExtensions
{
	/// <summary>
	/// Adds saga instrumentation for OpenTelemetry metrics and tracing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method enables OpenTelemetry instrumentation for saga operations:
	/// <list type="bullet">
	/// <item><description>Metrics from <see cref="SagaMetrics"/> (counters, histograms, gauges)</description></item>
	/// <item><description>Tracing from <see cref="SagaActivitySource"/> (distributed trace spans)</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// <b>OpenTelemetry Configuration:</b>
	/// To collect these metrics and traces, configure your OpenTelemetry SDK:
	/// <code>
	/// builder.Services.AddOpenTelemetry()
	///     .WithMetrics(metrics => metrics.AddMeter(SagaMetrics.MeterName))
	///     .WithTracing(tracing => tracing.AddSource(SagaActivitySource.SourceName));
	/// </code>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddSagaInstrumentation();
	///
	/// // Then configure OpenTelemetry to collect the metrics
	/// services.AddOpenTelemetry()
	///     .WithMetrics(metrics => metrics.AddMeter("Excalibur.Dispatch.Sagas"))
	///     .WithTracing(tracing => tracing.AddSource("Excalibur.Dispatch.Sagas"));
	/// </code>
	/// </example>
	public static IServiceCollection AddSagaInstrumentation(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register instrumentation options so consumers can configure metrics/tracing toggles
		_ = services.AddOptions<SagaInstrumentationOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Ensure the static meter and activity source are initialized.
		// These are process-lifetime singletons (static fields), but touching them here
		// guarantees they are created before any OpenTelemetry SDK listener starts collecting.
		_ = SagaMetrics.MeterName;
		_ = SagaActivitySource.SourceName;

		return services;
	}

	/// <summary>
	/// Adds saga instrumentation with custom configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure instrumentation options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSagaInstrumentation(
		this IServiceCollection services,
		Action<SagaInstrumentationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddSagaInstrumentation();
		_ = services.Configure(configure);

		return services;
	}
}
