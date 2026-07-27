// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.OpenSearch;
using Excalibur.Compliance;

using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

// Note: IAuditStore is NOT registered from this package.
// OpenSearch serves as a search/analytics sink, not a compliance-grade audit store.
// See ADR-290 for rationale.

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring OpenSearch audit services.
/// </summary>
public static class OpenSearchServiceCollectionExtensions
{
	/// <summary>
	/// Adds OpenSearch audit log exporter services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the OpenSearch audit builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddOpenSearchAuditExporter(os =&gt;
	/// {
	///     os.NodeUri(new Uri("https://my-cluster:9200"))
	///       .IndexName("dispatch-audit");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddOpenSearchAuditExporter(
		this IServiceCollection services,
		Action<IAuditLoggingOpenSearchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new OpenSearchExporterOptions
		{
			OpenSearchUrl = null!,
		};
		var builder = new AuditLoggingOpenSearchBuilder(options);
		configure(builder);

		RegisterExporterOptionsAndServices(services, builder, options);

		return services;
	}

	/// <summary>
	/// Adds the OpenSearch audit sink for real-time audit event indexing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the OpenSearch audit sink builder.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddOpenSearchAuditSink(
		this IServiceCollection services,
		Action<IAuditLoggingOpenSearchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new OpenSearchExporterOptions
		{
			OpenSearchUrl = null!,
		};
		var builder = new AuditLoggingOpenSearchBuilder(options);
		configure(builder);

		RegisterSinkOptionsAndServices(services, builder, options);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterExporterOptionsAndServices(
		IServiceCollection services,
		AuditLoggingOpenSearchBuilder builder,
		OpenSearchExporterOptions options)
	{
		_ = services.Configure<OpenSearchExporterOptions>(opt =>
		{
			opt.OpenSearchUrl = options.OpenSearchUrl;
			opt.NodeUrls = options.NodeUrls;
			opt.IndexPrefix = options.IndexPrefix;
			opt.BulkBatchSize = options.BulkBatchSize;
			opt.RefreshPolicy = options.RefreshPolicy;
			opt.ApiKey = options.ApiKey;
			opt.MaxRetryAttempts = options.MaxRetryAttempts;
			opt.RetryBaseDelay = options.RetryBaseDelay;
			opt.Timeout = options.Timeout;
			opt.ApplicationName = options.ApplicationName;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<OpenSearchExporterOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<OpenSearchExporterOptions>().ValidateOnStart();

		AddOpenSearchAuditExporterCore(services);
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterSinkOptionsAndServices(
		IServiceCollection services,
		AuditLoggingOpenSearchBuilder builder,
		OpenSearchExporterOptions options)
	{
		_ = services.Configure<OpenSearchAuditSinkOptions>(opt =>
		{
			opt.OpenSearchUrl = options.OpenSearchUrl;
			opt.NodeUrls = options.NodeUrls;
			opt.IndexPrefix = options.IndexPrefix;
			opt.RefreshPolicy = options.RefreshPolicy;
			opt.ApiKey = options.ApiKey;
			opt.MaxRetryAttempts = options.MaxRetryAttempts;
			opt.RetryBaseDelay = options.RetryBaseDelay;
			opt.Timeout = options.Timeout;
			opt.ApplicationName = options.ApplicationName;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<OpenSearchAuditSinkOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<OpenSearchAuditSinkOptions>().ValidateOnStart();

		AddOpenSearchAuditSinkCore(services);
	}

	private static void AddOpenSearchAuditExporterCore(IServiceCollection services)
	{
		_ = services.AddSingleton<IValidateOptions<OpenSearchExporterOptions>,
			OpenSearchExporterOptionsValidator>();

		// Transient-fault retry (formerly hand-rolled in the exporter) is delegated to the standard
		// Polly-backed resilience pipeline (Microsoft.Extensions.Http.Resilience). Per-attempt node
		// failover is preserved by an inner NodeFailoverHandler that re-selects the next cluster node on
		// every SendAsync, so each resilience retry re-enters it and targets a new node. The pipeline owns
		// timeouts, so the HttpClient timeout is left infinite.
		var httpClientBuilder = services.AddHttpClient<OpenSearchAuditExporter>(static (_, client) =>
		{
			client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		});

		// Resilience handler is OUTER, node-failover handler is INNER (added second), so each retry
		// re-enters the failover handler and round-robins to the next node.
		_ = httpClientBuilder.AddStandardResilienceHandler();

		_ = httpClientBuilder.AddHttpMessageHandler(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<OpenSearchExporterOptions>>().Value;
			var nodeUrls = options.NodeUrls is { Count: > 0 }
				? (IReadOnlyList<string>)options.NodeUrls
				: [options.OpenSearchUrl];
			return new NodeFailoverHandler(ToNodeUris(nodeUrls));
		});

		// Configure the standard resilience pipeline (keyed by the typed client's name) from the exporter's
		// retry options, so config-bound overrides flow through the DI options system.
		_ = services.AddOptions<HttpStandardResilienceOptions>(httpClientBuilder.Name)
			.Configure<IOptions<OpenSearchExporterOptions>>(static (resilience, exporterOptions) =>
				ConfigureResilience(resilience, exporterOptions.Value.MaxRetryAttempts,
					exporterOptions.Value.RetryBaseDelay, exporterOptions.Value.Timeout));

		_ = services.AddSingleton<IAuditLogExporter, OpenSearchAuditExporter>();
	}

	private static void AddOpenSearchAuditSinkCore(IServiceCollection services)
	{
		_ = services.AddSingleton<IValidateOptions<OpenSearchAuditSinkOptions>,
			OpenSearchAuditSinkOptionsValidator>();

		var httpClientBuilder = services.AddHttpClient<OpenSearchAuditSink>(static (_, client) =>
		{
			client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		});

		_ = httpClientBuilder.AddStandardResilienceHandler();

		_ = httpClientBuilder.AddHttpMessageHandler(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<OpenSearchAuditSinkOptions>>().Value;
			return new NodeFailoverHandler(ToNodeUris(options.GetResolvedNodeUrls()));
		});

		_ = services.AddOptions<HttpStandardResilienceOptions>(httpClientBuilder.Name)
			.Configure<IOptions<OpenSearchAuditSinkOptions>>(static (resilience, sinkOptions) =>
				ConfigureResilience(resilience, sinkOptions.Value.MaxRetryAttempts,
					sinkOptions.Value.RetryBaseDelay, sinkOptions.Value.Timeout));

		_ = services.AddSingleton<OpenSearchAuditSink>();
	}

	// Maps the exporter/sink retry options onto the standard resilience pipeline: exponential backoff
	// matching the former baseDelay * 2^(attempt-1), with the per-request timeout preserved as the
	// per-attempt timeout (the total-request timeout accommodates every attempt, and the breaker
	// sampling window must be at least twice the attempt timeout — standard-handler invariants).
	private static void ConfigureResilience(
		HttpStandardResilienceOptions resilience,
		int maxRetryAttempts,
		TimeSpan retryBaseDelay,
		TimeSpan requestTimeout)
	{
		resilience.Retry.MaxRetryAttempts = maxRetryAttempts;
		resilience.Retry.Delay = retryBaseDelay;
		resilience.Retry.BackoffType = Polly.DelayBackoffType.Exponential;

		resilience.AttemptTimeout.Timeout = requestTimeout;
		resilience.TotalRequestTimeout.Timeout = requestTimeout * (maxRetryAttempts + 1);
		resilience.CircuitBreaker.SamplingDuration = requestTimeout * 2;
	}

	private static Uri[] ToNodeUris(IReadOnlyList<string> nodeUrls) =>
		[.. nodeUrls.Select(static url => new Uri(url.TrimEnd('/'), UriKind.Absolute))];
}
