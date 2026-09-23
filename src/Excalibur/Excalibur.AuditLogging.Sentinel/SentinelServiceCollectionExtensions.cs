// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Sentinel;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Azure Sentinel audit exporter services.
/// </summary>
public static class SentinelServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Sentinel audit log exporter services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Sentinel audit builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddSentinelAuditExporter(sentinel =&gt;
	/// {
	///     sentinel.WorkspaceId("your-workspace-id")
	///             .SharedKey("your-shared-key");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddSentinelAuditExporter(
		this IServiceCollection services,
		Action<IAuditLoggingSentinelBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new SentinelExporterOptions
		{
			WorkspaceId = null!,
			SharedKey = null!,
		};
		var builder = new AuditLoggingSentinelBuilder(options);
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
		AuditLoggingSentinelBuilder builder,
		SentinelExporterOptions options)
	{
		_ = services.Configure<SentinelExporterOptions>(opt =>
		{
			opt.WorkspaceId = options.WorkspaceId;
			opt.SharedKey = options.SharedKey;
			opt.LogType = options.LogType;
			opt.AzureResourceId = options.AzureResourceId;
			opt.TimeGeneratedField = options.TimeGeneratedField;
			opt.MaxBatchSize = options.MaxBatchSize;
			opt.MaxRetryAttempts = options.MaxRetryAttempts;
			opt.RetryBaseDelay = options.RetryBaseDelay;
			opt.Timeout = options.Timeout;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<SentinelExporterOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<SentinelExporterOptions>().ValidateOnStart();

		RegisterSentinelAuditExporterCore(services);
	}

	private static void RegisterSentinelAuditExporterCore(IServiceCollection services)
	{
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SentinelExporterOptions>, SentinelExporterOptionsValidator>());

		// Transient-fault retry (formerly hand-rolled in the exporter) is delegated to the standard
		// Polly-backed resilience pipeline (Microsoft.Extensions.Http.Resilience), which retries the same
		// transient responses (408/429/5xx + HttpRequestException/timeout) the exporter used to classify.
		// The pipeline owns timeouts, so the HttpClient timeout is left infinite — the handler's per-attempt
		// and total-request timeouts bound each call instead.
		var httpClientBuilder = services.AddHttpClient<SentinelAuditExporter>(static (_, client) =>
		{
			client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		});

		_ = httpClientBuilder.AddStandardResilienceHandler();

		// Configure the standard resilience pipeline (keyed by the typed client's name) from the exporter's
		// retry options, so config-bound overrides flow through the DI options system.
		_ = services.AddOptions<HttpStandardResilienceOptions>(httpClientBuilder.Name)
			.Configure<IOptions<SentinelExporterOptions>>(static (resilience, exporterOptions) =>
			{
				var retry = exporterOptions.Value;

				resilience.Retry.MaxRetryAttempts = retry.MaxRetryAttempts;
				resilience.Retry.Delay = retry.RetryBaseDelay;
				resilience.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

				// Preserve the configured per-request timeout as the per-attempt timeout; the total-request
				// timeout must accommodate every attempt, and the breaker sampling window must be at least
				// twice the attempt timeout (standard-handler validation invariants).
				resilience.AttemptTimeout.Timeout = retry.Timeout;
				resilience.TotalRequestTimeout.Timeout = retry.Timeout * (retry.MaxRetryAttempts + 1);
				resilience.CircuitBreaker.SamplingDuration = retry.Timeout * 2;
			});

		// Delegate to the typed client rather than letting the container activate a second instance.
		// An implementation-type registration is constructed from the container's own HttpClient, not the
		// one AddHttpClient configured above -- so the exporter a consumer resolved carried none of that
		// configuration, and everything it sets was inert on the only path anything uses. Transient matches
		// the typed client's own lifetime, which keeps handler rotation working; the exporter holds no state.
		_ = services.AddTransient<IAuditLogExporter>(static sp => sp.GetRequiredService<SentinelAuditExporter>());
	}
}
