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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
		// Configure options. The delegate is registered once for DI resolution.
		var optionsBuilder = services.AddOptions<ContextObservabilityOptions>();
		if (configureOptions is not null)
		{
			_ = optionsBuilder.Configure(configureOptions);
		}

		_ = optionsBuilder
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ContextObservabilityOptions>,
				ContextObservabilityOptionsValidator>());

		// Read the Enabled flag eagerly to decide whether to wire OTel,
		// without calling BuildServiceProvider(). This creates a temporary
		// instance -- the delegate runs once here and once when DI resolves
		// IOptions<ContextObservabilityOptions>. The delegate MUST be
		// side-effect-free (pure configuration mapping only).
		var snapshot = new ContextObservabilityOptions();
		configureOptions?.Invoke(snapshot);

		// Register core observability services
		services.TryAddSingleton<IContextFlowTracker, ContextFlowTracker>();
		services.TryAddSingleton<IContextFlowMetrics, ContextFlowMetrics>();
		services.TryAddSingleton<IContextTraceEnricher, ContextTraceEnricher>();
		services.TryAddSingleton<IContextFlowDiagnostics, ContextFlowDiagnostics>();

		// Register PII sanitizer with options + startup warning validator
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateOnStart();
		services.TryAddSingleton<ITelemetrySanitizer, HashingTelemetrySanitizer>();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TelemetrySanitizerOptions>, TelemetrySanitizerOptionsValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TelemetrySanitizerOptions>, TelemetrySanitizerOptionsDataAnnotationsValidator>());

		// Flow IncludeRawPii into all IncludeSensitiveData flags
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Excalibur.Dispatch.Options.Core.TracingOptions>, SensitiveDataPostConfigureOptions>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Excalibur.Dispatch.Options.Middleware.AuditLoggingOptions>, SensitiveDataPostConfigureOptions>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<Excalibur.Dispatch.Observability.Metrics.ObservabilityOptions>, SensitiveDataPostConfigureOptions>());

		// Configure OpenTelemetry
		if (snapshot.Enabled)
		{
			ConfigureOpenTelemetry(services, snapshot);
		}

		return services;
	}

	/// <summary>
	/// Adds the context observability middleware to the dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder UseContextObservability(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ContextObservabilityMiddleware>();
		return builder.UseMiddleware<ContextObservabilityMiddleware>();
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
			.AddSource("Excalibur.Dispatch")
			.AddSource("Excalibur.Dispatch.*")
			.AddSource("Excalibur.Dispatch.BackgroundServices")
			.AddSource("Excalibur.Data.*")
			.AddSource("Excalibur.EventSourcing.*")
			.AddSource("Excalibur.LeaderElection")
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

		if (options.Export.EnableConsoleExporterInDevelopment)
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

		if (options.Export.EnableConsoleExporterInDevelopment)
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
			.ValidateOnStart();

		// Register the startup validator for custom patterns
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ComplianceTelemetrySanitizerOptions>,
				ComplianceTelemetrySanitizerOptionsValidator>());

		// Ensure base TelemetrySanitizerOptions are registered (for the inner HashingTelemetrySanitizer)
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<TelemetrySanitizerOptions>,
				TelemetrySanitizerOptionsDataAnnotationsValidator>());

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
