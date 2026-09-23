// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Domain;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring OpenTelemetry observability in an <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class ObservabilityHostApplicationBuilderExtensions
{
	/// <summary>
	/// Configures OpenTelemetry-based metrics with optional customizations.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <param name="configureMetrics"> An optional <see cref="Action{T}" /> to customize the <see cref="MeterProviderBuilder" />. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburMetrics(
		this IHostApplicationBuilder builder,
		Action<MeterProviderBuilder>? configureMetrics = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOpenTelemetry().WithMetrics(meterOptions =>
		{
			_ = meterOptions.AddMeter($"{builder.Environment.ApplicationName}_Metrics");
			_ = meterOptions.AddMeter("Excalibur.Metrics"); // Add our custom meter
			_ = meterOptions.AddAspNetCoreInstrumentation();
			_ = meterOptions.AddHttpClientInstrumentation();
			_ = meterOptions.AddRuntimeInstrumentation();
			_ = meterOptions.AddPrometheusExporter();
			_ = meterOptions.AddConsoleExporter();

			configureMetrics?.Invoke(meterOptions);
		});

		// The resource identity comes from the bound options -- the value the host validated at startup -- rather than from
		// the process-wide static, which a host configuring through the options pipeline never gets to influence. It has to be
		// deferred because a registration-time callback has no service provider to resolve the options from.
		//
		// ConfigureResource, not SetResourceBuilder: it AUGMENTS whatever resource builder is in effect instead of replacing
		// it, so a consumer who supplied their own through configureMetrics keeps it.
		builder.Services.ConfigureOpenTelemetryMeterProvider(static (provider, meterOptions) =>
			_ = meterOptions.ConfigureResource(resource =>
				_ = resource.AddService(
					provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value.ApplicationSystemName)));

		return builder;
	}

	/// <summary>
	/// Configures OpenTelemetry-based tracing with optional customizations.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <param name="configureTracing"> An optional <see cref="Action{T}" /> to customize the <see cref="TracerProviderBuilder" />. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburTracing(
		this IHostApplicationBuilder builder,
		Action<TracerProviderBuilder>? configureTracing = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOpenTelemetry().WithTracing(tracerOptions =>
		{
			_ = tracerOptions.AddAspNetCoreInstrumentation();
			_ = tracerOptions.AddConsoleExporter();

			configureTracing?.Invoke(tracerOptions);
		});

		// See ConfigureExcaliburMetrics for why this is deferred and why it augments rather than replaces.
		builder.Services.ConfigureOpenTelemetryTracerProvider(static (provider, tracerOptions) =>
			_ = tracerOptions.ConfigureResource(resource =>
				_ = resource.AddService(
					provider.GetRequiredService<IOptions<ApplicationContextOptions>>().Value.ApplicationSystemName)));

		return builder;
	}
}
