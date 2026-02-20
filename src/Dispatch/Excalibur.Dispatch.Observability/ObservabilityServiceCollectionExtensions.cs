// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability;
using Excalibur.Dispatch.Observability.Context;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering observability services.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
	/// <summary>
	/// Adds comprehensive context flow observability to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section for observability options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types")]
	[RequiresDynamicCode("Configuration binding may require dynamic code generation")]
	public static IServiceCollection AddDispatchObservability(
		this IServiceCollection services,
		IConfiguration configuration) =>
		services.AddDispatchObservability(options => configuration.GetSection("Dispatch:Observability").Bind(options));

	/// <summary>
	/// Adds comprehensive context flow observability to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure observability options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddDispatchObservability(
		this IServiceCollection services,
		Action<ContextObservabilityOptions>? configureOptions = null)
	{
		// Configure options
		var optionsBuilder = services.AddOptions<ContextObservabilityOptions>();
		if (configureOptions != null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register core observability services
		services.TryAddSingleton<IContextFlowTracker, ContextFlowTracker>();
		services.TryAddSingleton<IContextFlowMetrics, ContextFlowMetrics>();
		services.TryAddSingleton<IContextTraceEnricher, ContextTraceEnricher>();
		services.TryAddSingleton<IContextFlowDiagnostics, ContextFlowDiagnostics>();

		// Register PII sanitizer with options + startup warning validator
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ITelemetrySanitizer, HashingTelemetrySanitizer>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TelemetrySanitizerOptions>, TelemetrySanitizerOptionsValidator>());

		// Flow IncludeRawPii into all IncludeSensitiveData flags
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Excalibur.Dispatch.Options.Core.TracingOptions>, SensitiveDataPostConfigureOptions>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Excalibur.Dispatch.Options.Middleware.AuditLoggingOptions>, SensitiveDataPostConfigureOptions>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Excalibur.Dispatch.Observability.Metrics.ObservabilityOptions>, SensitiveDataPostConfigureOptions>());

		// Apply options to determine OTel configuration without BuildServiceProvider()
		var options = new ContextObservabilityOptions();
		configureOptions?.Invoke(options);

		// Configure OpenTelemetry
		if (options.Enabled)
		{
			ConfigureOpenTelemetry(services, options);
		}

		return services;
	}

	/// <summary>
	/// Adds the context observability middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddContextObservability(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ContextObservabilityMiddleware>();
		_ = builder.Services.AddMiddleware<ContextObservabilityMiddleware>();
		return builder;
	}

	[SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
		Justification = "OpenTelemetry configuration requires coupling with many types")]
	private static void ConfigureOpenTelemetry(IServiceCollection services, ContextObservabilityOptions options)
	{
		var resourceBuilder = ConfigureResourceBuilder(options);

		_ = services.AddOpenTelemetry()
			.WithTracing(tracerProviderBuilder => ConfigureTracing(tracerProviderBuilder, resourceBuilder, options))
			.WithMetrics(meterProviderBuilder => ConfigureMetrics(meterProviderBuilder, resourceBuilder, options));

		ConfigureLogging(services, resourceBuilder, options);
		ConfigureApplicationInsights(services, options);
	}

	private static ResourceBuilder ConfigureResourceBuilder(ContextObservabilityOptions options)
	{
		var resourceBuilder = ResourceBuilder
			.CreateDefault()
			.AddService(
				serviceName: options.Export.ServiceName,
				serviceVersion: options.Export.ServiceVersion)
			.AddTelemetrySdk();

		foreach (var attribute in options.Export.ResourceAttributes)
		{
			_ = resourceBuilder.AddAttributes(new[] { new KeyValuePair<string, object>(attribute.Key, attribute.Value) });
		}

		return resourceBuilder;
	}

	private static void ConfigureTracing(
		TracerProviderBuilder tracerProviderBuilder,
		ResourceBuilder resourceBuilder,
		ContextObservabilityOptions options)
	{
		_ = tracerProviderBuilder
			.SetResourceBuilder(resourceBuilder)
			.AddSource("Excalibur.Dispatch.Observability.*")
			.AddAspNetCoreInstrumentation(options =>
			{
				options.RecordException = true;
				options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
			})
			// R0.8: Suppress CA2000 - TracerProvider takes ownership and disposes processor/exporter; ServiceProvider lifecycle managed
#pragma warning disable CA2000
			.AddProcessor(sp => new BatchActivityExportProcessor(
				new ContextEnrichingExporter(sp)));
#pragma warning restore CA2000

		if (!string.IsNullOrWhiteSpace(options.Export.OtlpEndpoint))
		{
			_ = tracerProviderBuilder.AddOtlpExporter(otlpOptions =>
			{
				otlpOptions.Endpoint = new Uri(options.Export.OtlpEndpoint);
				otlpOptions.Protocol = OtlpExportProtocol.Grpc;
			});
		}

		if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.Ordinal))
		{
			_ = tracerProviderBuilder.AddConsoleExporter();
		}
	}

	private static void ConfigureMetrics(
		MeterProviderBuilder meterProviderBuilder,
		ResourceBuilder resourceBuilder,
		ContextObservabilityOptions options)
	{
		_ = meterProviderBuilder
			.SetResourceBuilder(resourceBuilder)
			.AddMeter("Excalibur.Dispatch.*")
			.AddRuntimeInstrumentation()
			.AddProcessInstrumentation()
			.AddAspNetCoreInstrumentation()
			.AddView(
				"context.flow.*",
				new ExplicitBucketHistogramConfiguration { Boundaries = [0, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000] });

		if (options.Export.ExportToPrometheus)
		{
			_ = meterProviderBuilder.AddPrometheusExporter();
		}

		if (!string.IsNullOrWhiteSpace(options.Export.OtlpEndpoint))
		{
			_ = meterProviderBuilder.AddOtlpExporter(otlpOptions =>
			{
				otlpOptions.Endpoint = new Uri(options.Export.OtlpEndpoint);
				otlpOptions.Protocol = OtlpExportProtocol.Grpc;
			});
		}

		if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.Ordinal))
		{
			_ = meterProviderBuilder.AddConsoleExporter();
		}
	}

	private static void ConfigureLogging(
		IServiceCollection services,
		ResourceBuilder resourceBuilder,
		ContextObservabilityOptions options) => _ = services.AddLogging(loggingBuilder => loggingBuilder.AddOpenTelemetry(loggingOptions =>
													 {
														 _ = loggingOptions.SetResourceBuilder(resourceBuilder);
														 loggingOptions.IncludeScopes = true;
														 loggingOptions.IncludeFormattedMessage = true;

														 if (!string.IsNullOrWhiteSpace(options.Export.OtlpEndpoint))
														 {
															 _ = loggingOptions.AddOtlpExporter(otlpOptions =>
															 {
																 otlpOptions.Endpoint = new Uri(options.Export.OtlpEndpoint);
																 otlpOptions.Protocol = OtlpExportProtocol.Grpc;
															 });
														 }
													 }));

	/// <summary>
	/// Adds compliance-level telemetry sanitization that detects and redacts PII patterns
	/// (emails, phone numbers, SSNs) from telemetry tags and payloads.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Optional action to configure compliance sanitizer options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method replaces the default <see cref="HashingTelemetrySanitizer"/> with a
	/// <see cref="ComplianceTelemetrySanitizer"/> that layers compliance-specific pattern detection
	/// on top of the baseline hashing sanitizer. The baseline <see cref="TelemetrySanitizerOptions"/>
	/// are still respected.
	/// </para>
	/// <para>
	/// Call this method <strong>after</strong> <see cref="AddDispatchObservability(IServiceCollection, Action{ContextObservabilityOptions}?)"/>
	/// to override the default sanitizer.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddComplianceTelemetrySanitizer(
		this IServiceCollection services,
		Action<ComplianceTelemetrySanitizerOptions>? configureOptions = null)
	{
		var optionsBuilder = services.AddOptions<ComplianceTelemetrySanitizerOptions>();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register the startup validator for custom patterns
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ComplianceTelemetrySanitizerOptions>,
				ComplianceTelemetrySanitizerOptionsValidator>());

		// Ensure base TelemetrySanitizerOptions are registered (for the inner HashingTelemetrySanitizer)
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Replace the default ITelemetrySanitizer with the compliance-aware one
		services.RemoveAll<ITelemetrySanitizer>();
		services.AddSingleton<ITelemetrySanitizer, ComplianceTelemetrySanitizer>();

		return services;
	}

	private static void ConfigureApplicationInsights(IServiceCollection services, ContextObservabilityOptions options)
	{
		if (options.Export.ExportToApplicationInsights && !string.IsNullOrWhiteSpace(options.Export.ApplicationInsightsConnectionString))
		{
			_ = services.AddApplicationInsightsTelemetry(telemetryOptions =>
			{
				telemetryOptions.ConnectionString = options.Export.ApplicationInsightsConnectionString;
				telemetryOptions.EnableRequestTrackingTelemetryModule = true;
				telemetryOptions.EnableDependencyTrackingTelemetryModule = true;
				telemetryOptions.EnablePerformanceCounterCollectionModule = true;
				telemetryOptions.EnableEventCounterCollectionModule = true;
				telemetryOptions.EnableAdaptiveSampling = false; // We want all context flow data
			});
		}
	}
}
