// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Splunk;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Splunk audit exporter services.
/// </summary>
public static class SplunkServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Splunk audit log exporter to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Splunk audit builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddSplunkAuditExporter(splunk =&gt;
	/// {
	///     splunk.HecEndpoint(new Uri("https://splunk:8088/services/collector"))
	///           .HecToken("your-hec-token")
	///           .Index("audit")
	///           .SourceType("audit:dispatch");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddSplunkAuditExporter(
		this IServiceCollection services,
		Action<IAuditLoggingSplunkBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SplunkExporterOptions();
		var builder = new AuditLoggingSplunkBuilder(options);
		configure(builder);

		RegisterOptionsAndServices(services, builder, options);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		AuditLoggingSplunkBuilder builder,
		SplunkExporterOptions options)
	{
		_ = services.Configure<SplunkExporterOptions>(opt =>
		{
			opt.Index = options.Index;
			opt.SourceType = options.SourceType;
			opt.Source = options.Source;
			opt.Host = options.Host;
			opt.UseAck = options.UseAck;
			opt.Channel = options.Channel;
			opt.Connection = options.Connection;
			opt.Batch = options.Batch;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<SplunkExporterOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<SplunkExporterOptions>().ValidateOnStart();

		RegisterSplunkAuditExporterCore(services);
	}

	private static void RegisterSplunkAuditExporterCore(IServiceCollection services)
	{
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SplunkExporterOptions>>(new SplunkExporterOptionsValidator()));
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SplunkBatchOptions>, SplunkBatchOptionsValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SplunkConnectionOptions>, SplunkConnectionOptionsValidator>());

		// Transient-fault retry (formerly hand-rolled in the exporter) is delegated to the standard
		// Polly-backed resilience pipeline (Microsoft.Extensions.Http.Resilience); the pipeline owns
		// timeouts, so the HttpClient timeout is left infinite.
		var httpClientBuilder = services.AddHttpClient<SplunkAuditExporter>(static (_, client) =>
			{
				client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
			})
			.ConfigurePrimaryHttpMessageHandler(static sp =>
			{
				var options = sp.GetRequiredService<IOptions<SplunkExporterOptions>>().Value;

				var handler = new HttpClientHandler();

				if (!options.Connection.ValidateCertificate)
				{
					handler.ServerCertificateCustomValidationCallback =
						HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
				}

				return handler;
			});

		_ = httpClientBuilder.AddStandardResilienceHandler();

		// Configure the standard resilience pipeline (keyed by the typed client's name) from the exporter's
		// batch retry options, so config-bound overrides flow through the DI options system.
		_ = services.AddOptions<HttpStandardResilienceOptions>(httpClientBuilder.Name)
			.Configure<IOptions<SplunkExporterOptions>>(static (resilience, exporterOptions) =>
			{
				var batch = exporterOptions.Value.Batch;

				resilience.Retry.MaxRetryAttempts = batch.MaxRetryAttempts;
				resilience.Retry.Delay = batch.RetryBaseDelay;
				resilience.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

				// Preserve the configured per-request timeout as the per-attempt timeout; the total-request
				// timeout must accommodate every attempt, and the breaker sampling window must be at least
				// twice the attempt timeout (standard-handler validation invariants).
				resilience.AttemptTimeout.Timeout = batch.RequestTimeout;
				resilience.TotalRequestTimeout.Timeout = batch.RequestTimeout * (batch.MaxRetryAttempts + 1);
				resilience.CircuitBreaker.SamplingDuration = batch.RequestTimeout * 2;
			});

		services.TryAddSingleton<IAuditLogExporter, SplunkAuditExporter>();
	}
}
