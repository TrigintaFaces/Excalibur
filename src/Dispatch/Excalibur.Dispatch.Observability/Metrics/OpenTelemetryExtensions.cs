// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Diagnostics;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Provides OpenTelemetry extensions for Dispatch metrics and tracing.
/// </summary>
/// <remarks>
/// <para>
/// This class provides extension methods for integrating Dispatch metrics and tracing with OpenTelemetry.
/// Use <see cref="AddAllDispatchMetrics(IOpenTelemetryBuilder)"/> to register all framework meters
/// and <see cref="AddAllDispatchTracing(IOpenTelemetryBuilder)"/> to register all ActivitySources.
/// </para>
/// </remarks>
public static class OpenTelemetryExtensions
{
	/// <summary>
	/// All known meter names across the Excalibur framework.
	/// </summary>
	/// <remarks>
	/// Meter names from external packages (LeaderElection, EventSourcing, Data, etc.) are
	/// listed as string literals since this project does not reference those assemblies directly.
	/// </remarks>
	private static readonly string[] AllMeterNames =
	[
		// Dispatch Core (from DispatchTelemetryConstants)
		DispatchTelemetryConstants.Meters.Core,       // "Excalibur.Dispatch.Core"
		DispatchTelemetryConstants.Meters.Pipeline,    // "Excalibur.Dispatch.Pipeline"
		DispatchTelemetryConstants.Meters.TimePolicy,  // "Excalibur.Dispatch.TimePolicy"

		// Dispatch Transport (from TransportMeter)
		TransportMeter.MeterName,                      // "Excalibur.Dispatch.Transport"

		// Dispatch Observability (local)
		DeadLetterQueueMetrics.MeterName,              // "Excalibur.Dispatch.DeadLetterQueue"
		CircuitBreakerMetrics.MeterName,               // "Excalibur.Dispatch.CircuitBreaker"

		// Dispatch Streaming (from StreamingHandlerTelemetryConstants)
		StreamingHandlerTelemetryConstants.MeterName,  // "Excalibur.Dispatch.Streaming"

		// Dispatch Compliance (external package — string literals)
		"Excalibur.Dispatch.Compliance",               // ComplianceMetrics.MeterName
		"Excalibur.Dispatch.Compliance.Erasure",       // ErasureTelemetryConstants.MeterName
		"Excalibur.Dispatch.Encryption",               // EncryptionTelemetry.MeterName

		// Excalibur EventSourcing (external package)
		"Excalibur.EventSourcing.MaterializedViews",   // MaterializedViewMetrics.MeterName

		// Excalibur Data (external packages)
		"Excalibur.Dispatch.WriteStores",              // WriteStoreTelemetry.MeterName
		"Excalibur.Data.Cdc",                          // CdcTelemetryConstants.MeterName
		"Excalibur.Data.Audit",                        // AuditTelemetryConstants.MeterName

		// Excalibur LeaderElection (external package)
		"Excalibur.LeaderElection",                    // LeaderElectionTelemetryConstants.MeterName

		// Excalibur Saga (external package)
		"Excalibur.Dispatch.Sagas",                    // SagaMetrics.MeterName

		// Excalibur Background Services (external package)
		"Excalibur.Dispatch.BackgroundServices",       // BackgroundServiceMetrics.MeterName

		// Dispatch Core (from DispatchTelemetryConstants — BatchProcessor)
		DispatchTelemetryConstants.Meters.BatchProcessor,  // "Excalibur.Dispatch.BatchProcessor"

		// Dispatch Observability (local — ContextFlowMetrics)
		Diagnostics.ContextObservabilityTelemetryConstants.MeterName,  // "Excalibur.Dispatch.Observability.Context"
	];

	/// <summary>
	/// All known ActivitySource names across the Excalibur framework.
	/// </summary>
	private static readonly string[] AllActivitySourceNames =
	[
		// Dispatch Core (from DispatchTelemetryConstants)
		DispatchTelemetryConstants.ActivitySources.Core,       // "Excalibur.Dispatch.Core"
		DispatchTelemetryConstants.ActivitySources.Pipeline,    // "Excalibur.Dispatch.Pipeline"
		DispatchTelemetryConstants.ActivitySources.TimePolicy,  // "Excalibur.Dispatch.TimePolicy"

		// Dispatch Streaming (from StreamingHandlerTelemetryConstants)
		StreamingHandlerTelemetryConstants.ActivitySourceName,  // "Excalibur.Dispatch.Streaming"

		// Dispatch Compliance (external package)
		"Excalibur.Dispatch.Compliance.Erasure",               // ErasureTelemetryConstants.ActivitySourceName

		// Excalibur Data (external packages)
		"Excalibur.Data.Cdc",                                  // CdcTelemetryConstants.ActivitySourceName
		"Excalibur.Data.Audit",                                // AuditTelemetryConstants.ActivitySourceName

		// Excalibur LeaderElection (external package)
		"Excalibur.LeaderElection",                            // LeaderElectionTelemetryConstants.ActivitySourceName
	];

	/// <summary>
	/// Adds Dispatch core metrics to OpenTelemetry configuration.
	/// </summary>
	/// <param name="builder">The OpenTelemetry builder.</param>
	/// <returns>The OpenTelemetry builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <remarks>
	/// <para>
	/// This adds the <c>Excalibur.Dispatch.Core</c> meter which includes:
	/// <list type="bullet">
	/// <item><description><c>dispatch.messages.processed</c> - Counter of processed messages</description></item>
	/// <item><description><c>dispatch.messages.duration</c> - Histogram of processing duration</description></item>
	/// <item><description><c>dispatch.messages.published</c> - Counter of published messages</description></item>
	/// <item><description><c>dispatch.messages.failed</c> - Counter of failed messages</description></item>
	/// <item><description><c>dispatch.sessions.active</c> - UpDownCounter of active sessions</description></item>
	/// </list>
	/// </para>
	/// </remarks>
	public static IOpenTelemetryBuilder AddDispatchMetrics(this IOpenTelemetryBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithMetrics(static metricsBuilder => _ = metricsBuilder.AddMeter(DispatchMetrics.MeterName));
	}

	/// <summary>
	/// Adds Dispatch core metrics to MeterProviderBuilder configuration.
	/// </summary>
	/// <param name="builder">The MeterProviderBuilder.</param>
	/// <returns>The MeterProviderBuilder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	public static MeterProviderBuilder AddDispatchMetrics(this MeterProviderBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddMeter(DispatchMetrics.MeterName);
	}

	/// <summary>
	/// Adds transport metrics to OpenTelemetry configuration.
	/// </summary>
	/// <param name="builder">The OpenTelemetry builder.</param>
	/// <returns>The OpenTelemetry builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <remarks>
	/// <para>
	/// This adds the <c>Excalibur.Dispatch.Transport</c> meter which includes:
	/// <list type="bullet">
	/// <item><description><c>dispatch.transport.messages_sent_total</c> - Counter of messages sent</description></item>
	/// <item><description><c>dispatch.transport.messages_received_total</c> - Counter of messages received</description></item>
	/// <item><description><c>dispatch.transport.errors_total</c> - Counter of transport errors</description></item>
	/// <item><description><c>dispatch.transport.send_duration_ms</c> - Histogram of send durations</description></item>
	/// <item><description><c>dispatch.transport.receive_duration_ms</c> - Histogram of receive durations</description></item>
	/// <item><description><c>dispatch.transport.starts_total</c> - Counter of transport starts</description></item>
	/// <item><description><c>dispatch.transport.stops_total</c> - Counter of transport stops</description></item>
	/// <item><description><c>dispatch.transport.connection_status</c> - Gauge of connection status</description></item>
	/// <item><description><c>dispatch.transport.pending_messages</c> - Gauge of pending messages</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// All transport metrics include <c>transport_name</c> and <c>transport_type</c> tags.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .AddTransportMetrics();
	/// </code>
	/// </example>
	public static IOpenTelemetryBuilder AddTransportMetrics(this IOpenTelemetryBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithMetrics(static metricsBuilder =>
			_ = metricsBuilder.AddMeter(TransportMeter.MeterName));
	}

	/// <summary>
	/// Adds transport metrics to MeterProviderBuilder configuration.
	/// </summary>
	/// <param name="builder">The MeterProviderBuilder.</param>
	/// <returns>The MeterProviderBuilder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <example>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithMetrics(builder => builder
	///         .AddTransportMetrics()
	///         .AddPrometheusExporter());
	/// </code>
	/// </example>
	public static MeterProviderBuilder AddTransportMetrics(this MeterProviderBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddMeter(TransportMeter.MeterName);
	}

	/// <summary>
	/// Adds all Excalibur framework metrics to OpenTelemetry configuration.
	/// </summary>
	/// <param name="builder">The OpenTelemetry builder.</param>
	/// <returns>The OpenTelemetry builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <remarks>
	/// <para>
	/// Registers all known meters across the Excalibur framework including:
	/// Core, Pipeline, TimePolicy, Transport, DeadLetterQueue, CircuitBreaker, Streaming,
	/// Compliance, Erasure, Encryption, EventSourcing, CDC, Audit, LeaderElection, Sagas,
	/// BackgroundServices, BatchProcessor, and Context observability.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .AddAllDispatchMetrics();
	/// </code>
	/// </example>
	public static IOpenTelemetryBuilder AddAllDispatchMetrics(this IOpenTelemetryBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithMetrics(static metricsBuilder =>
			_ = metricsBuilder.AddMeter(AllMeterNames));
	}

	/// <summary>
	/// Adds all Excalibur framework metrics to MeterProviderBuilder configuration.
	/// </summary>
	/// <param name="builder">The MeterProviderBuilder.</param>
	/// <returns>The MeterProviderBuilder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <example>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithMetrics(builder => builder
	///         .AddAllDispatchMetrics()
	///         .AddPrometheusExporter());
	/// </code>
	/// </example>
	public static MeterProviderBuilder AddAllDispatchMetrics(this MeterProviderBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddMeter(AllMeterNames);
	}

	/// <summary>
	/// Adds all Excalibur framework tracing ActivitySources to OpenTelemetry configuration.
	/// </summary>
	/// <param name="builder">The OpenTelemetry builder.</param>
	/// <returns>The OpenTelemetry builder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <remarks>
	/// <para>
	/// Registers all known ActivitySource names across the Excalibur framework including:
	/// Core, Pipeline, TimePolicy, Streaming, Erasure, CDC, Audit, and LeaderElection.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .AddAllDispatchTracing();
	/// </code>
	/// </example>
	public static IOpenTelemetryBuilder AddAllDispatchTracing(this IOpenTelemetryBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.WithTracing(static tracerBuilder =>
			_ = tracerBuilder.AddSource(AllActivitySourceNames));
	}

	/// <summary>
	/// Adds all Excalibur framework tracing ActivitySources to TracerProviderBuilder configuration.
	/// </summary>
	/// <param name="builder">The TracerProviderBuilder.</param>
	/// <returns>The TracerProviderBuilder for method chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when builder is null.</exception>
	/// <example>
	/// <code>
	/// services.AddOpenTelemetry()
	///     .WithTracing(builder => builder
	///         .AddAllDispatchTracing()
	///         .AddOtlpExporter());
	/// </code>
	/// </example>
	public static TracerProviderBuilder AddAllDispatchTracing(this TracerProviderBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder.AddSource(AllActivitySourceNames);
	}
}
