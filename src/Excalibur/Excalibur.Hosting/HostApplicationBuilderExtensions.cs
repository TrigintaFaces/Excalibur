// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Domain;
using Excalibur.Domain.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;
using Serilog.Core;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring application-specific services and features in an <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	/// Configures the <see cref="ApplicationContext" /> with settings from the application configuration and environment.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureApplicationContext(this IHostApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var configContext = builder.Configuration.GetApplicationContextConfiguration();

		// Add default values if not present in configuration
		_ = configContext.TryAdd("ApplicationName", builder.Environment.ApplicationName);
		_ = configContext.TryAdd("ApplicationSystemName", builder.Environment.ApplicationName.ToKebabCaseLower(clean: true));

		ApplicationContext.Init(configContext);

		// Also register IOptions<ApplicationContextOptions> for DI-based access
		builder.Services.AddApplicationContext(builder.Configuration);

		Log.Information("ApplicationContext initialized successfully with values: {ConfigContext}", configContext);

		return builder;
	}

	/// <summary>
	/// Registers <see cref="ApplicationContextOptions"/> with the DI container, bound from the
	/// <c>ApplicationContext</c> configuration section. Consumers can inject
	/// <see cref="Options.IOptions{ApplicationContextOptions}"/> instead
	/// of using the static <see cref="ApplicationContext"/> API.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
	[RequiresUnreferencedCode(
		"Configuration binding may reference types not preserved during trimming.")]
	[RequiresDynamicCode(
		"Configuration binding for ApplicationContextOptions requires dynamic code generation.")]
	public static IServiceCollection AddApplicationContext(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ApplicationContextOptions>()
			.Bind(configuration.GetSection(nameof(ApplicationContext)))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services;
	}

	/// <summary>
	/// Configures Serilog-based logging with OpenTelemetry support.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <param name="additionalLogSinks"> The additional sinks to configure the <see cref="LoggerConfiguration" /> to write Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburLogging(
		this IHostApplicationBuilder builder,
		params ILogEventSink[]? additionalLogSinks)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var loggerConfig = new LoggerConfiguration()
			.ReadFrom.Configuration(builder.Configuration)
			.Enrich.FromLogContext()
			.Enrich.WithProperty("ApplicationName", builder.Environment.ApplicationName)
			.Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

		foreach (var sink in additionalLogSinks ?? [])
		{
			_ = loggerConfig.WriteTo.Sink(sink);
		}

		Log.Logger = loggerConfig.CreateLogger();

		_ = builder.Logging.ClearProviders();
		_ = builder.Logging.AddSerilog(Log.Logger, dispose: true);
		_ = builder.Logging.AddOpenTelemetry(static loggerOptions =>
		{
			loggerOptions.IncludeFormattedMessage = true;
			_ = loggerOptions.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ApplicationContext.ApplicationSystemName));
			_ = loggerOptions.AddConsoleExporter();
		});

		_ = builder.Services.AddSerilog(Log.Logger, dispose: true);

		Log.Information("Serilog logging configured successfully.");

		return builder;
	}

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

		// Add Excalibur metrics services (IMetrics implementation)
		_ = builder.Services.AddExcaliburMetrics();

		_ = builder.Services.AddOpenTelemetry().WithMetrics(meterOptions =>
		{
			_ = meterOptions.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ApplicationContext.ApplicationSystemName));
			_ = meterOptions.AddMeter($"{builder.Environment.ApplicationName}_Metrics");
			_ = meterOptions.AddMeter("Excalibur.Metrics"); // Add our custom meter
			_ = meterOptions.AddAspNetCoreInstrumentation();
			_ = meterOptions.AddHttpClientInstrumentation();
			_ = meterOptions.AddRuntimeInstrumentation();
			_ = meterOptions.AddPrometheusExporter();
			_ = meterOptions.AddConsoleExporter();

			configureMetrics?.Invoke(meterOptions);
		});

		Log.Information("OpenTelemetry metrics configured successfully.");

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
			_ = tracerOptions.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ApplicationContext.ApplicationSystemName));
			_ = tracerOptions.AddAspNetCoreInstrumentation();
			_ = tracerOptions.AddConsoleExporter();

			configureTracing?.Invoke(tracerOptions);
		});

		Log.Information("OpenTelemetry tracing configured successfully.");

		return builder;
	}
}
