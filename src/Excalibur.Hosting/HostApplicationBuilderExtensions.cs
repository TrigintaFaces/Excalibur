using Excalibur.Core;
using Excalibur.Core.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

namespace Excalibur.Hosting;

/// <summary>
///     Provides extension methods for configuring application-specific services and features in an <see cref="IHostApplicationBuilder" />.
/// </summary>
public static class HostApplicationBuilderExtensions
{
	/// <summary>
	///     Configures the <see cref="ApplicationContext" /> with settings from the application configuration and environment.
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
		_ = configContext.TryAdd("ApplicationSystemName", builder.Environment.ApplicationName.ToKebabCaseLower(true));

		ApplicationContext.Init(configContext);

		Log.Information("ApplicationContext initialized successfully with values: {ConfigContext}", configContext);

		return builder;
	}

	/// <summary>
	///     Configures Serilog-based logging with OpenTelemetry support.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburLogging(this IHostApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		Log.Logger = new LoggerConfiguration()
			.ReadFrom.Configuration(builder.Configuration)
			.Enrich.FromLogContext()
			.Enrich.WithProperty("ApplicationName", builder.Environment.ApplicationName)
			.Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
			.CreateLogger();

		_ = builder.Logging.ClearProviders();
		_ = builder.Logging.AddSerilog(Log.Logger, dispose: true);
		_ = builder.Logging.AddOpenTelemetry(loggerOptions =>
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
	///     Configures OpenTelemetry-based metrics with optional customizations.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <param name="configureMetrics"> An optional <see cref="Action{T}" /> to customize the <see cref="MeterProviderBuilder" />. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburMetrics(this IHostApplicationBuilder builder,
		Action<MeterProviderBuilder>? configureMetrics = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddOpenTelemetry().WithMetrics(meterOptions =>
		{
			_ = meterOptions.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ApplicationContext.ApplicationSystemName));
			_ = meterOptions.AddMeter($"{builder.Environment.ApplicationName}_Metrics");
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
	///     Configures OpenTelemetry-based tracing with optional customizations.
	/// </summary>
	/// <param name="builder"> The <see cref="IHostApplicationBuilder" /> to configure. </param>
	/// <param name="configureTracing"> An optional <see cref="Action{T}" /> to customize the <see cref="TracerProviderBuilder" />. </param>
	/// <returns> The updated <see cref="IHostApplicationBuilder" /> instance for further configuration. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="builder" /> is null. </exception>
	public static IHostApplicationBuilder ConfigureExcaliburTracing(this IHostApplicationBuilder builder,
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
