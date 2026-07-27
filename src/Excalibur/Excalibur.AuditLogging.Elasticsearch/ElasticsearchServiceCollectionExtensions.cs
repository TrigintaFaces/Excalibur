// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Elasticsearch;
using Excalibur.Compliance;

using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

// Note: IAuditStore is NOT registered from this package.
// Elasticsearch serves as a search/analytics sink, not a compliance-grade audit store.
// See ADR-290 for rationale.

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch audit services.
/// </summary>
public static class ElasticsearchServiceCollectionExtensions
{
	/// <summary>
	/// Adds Elasticsearch audit log exporter services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Elasticsearch audit builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddElasticsearchAuditExporter(es =&gt;
	/// {
	///     es.NodeUri(new Uri("https://my-cluster:9200"))
	///       .IndexName("dispatch-audit");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddElasticsearchAuditExporter(
		this IServiceCollection services,
		Action<IAuditLoggingElasticsearchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ElasticsearchExporterOptions
		{
			ElasticsearchUrl = null!,
		};
		var builder = new AuditLoggingElasticsearchBuilder(options);
		configure(builder);

		RegisterExporterOptionsAndServices(services, builder, options);

		return services;
	}

	/// <summary>
	/// Adds the Elasticsearch audit sink for real-time audit event indexing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Elasticsearch audit sink builder.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddElasticsearchAuditSink(
		this IServiceCollection services,
		Action<IAuditLoggingElasticsearchBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ElasticsearchExporterOptions
		{
			ElasticsearchUrl = null!,
		};
		var builder = new AuditLoggingElasticsearchBuilder(options);
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
		AuditLoggingElasticsearchBuilder builder,
		ElasticsearchExporterOptions options)
	{
		_ = services.Configure<ElasticsearchExporterOptions>(opt =>
		{
			opt.ElasticsearchUrl = options.ElasticsearchUrl;
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
			_ = services.AddOptions<ElasticsearchExporterOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<ElasticsearchExporterOptions>().ValidateOnStart();

		AddElasticsearchAuditExporterCore(services);
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterSinkOptionsAndServices(
		IServiceCollection services,
		AuditLoggingElasticsearchBuilder builder,
		ElasticsearchExporterOptions options)
	{
		_ = services.Configure<ElasticsearchAuditSinkOptions>(opt =>
		{
			opt.ElasticsearchUrl = options.ElasticsearchUrl;
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
			_ = services.AddOptions<ElasticsearchAuditSinkOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<ElasticsearchAuditSinkOptions>().ValidateOnStart();

		AddElasticsearchAuditSinkCore(services);
	}

	private static void AddElasticsearchAuditExporterCore(IServiceCollection services)
	{
		_ = services.AddSingleton<IValidateOptions<ElasticsearchExporterOptions>,
			ElasticsearchExporterOptionsValidator>();

		// Transient-fault retry (formerly hand-rolled in the exporter) is delegated to the standard
		// Polly-backed resilience pipeline (Microsoft.Extensions.Http.Resilience). Per-attempt node
		// failover is preserved by an inner NodeFailoverHandler that re-selects the next cluster node on
		// every SendAsync, so each resilience retry re-enters it and targets a new node. The pipeline owns
		// timeouts, so the HttpClient timeout is left infinite.
		var httpClientBuilder = services.AddHttpClient<ElasticsearchAuditExporter>(static (_, client) =>
		{
			client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		});

		// Resilience handler is OUTER, node-failover handler is INNER (added second), so each retry
		// re-enters the failover handler and round-robins to the next node.
		_ = httpClientBuilder.AddStandardResilienceHandler();

		_ = httpClientBuilder.AddHttpMessageHandler(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchExporterOptions>>().Value;
			var nodeUrls = options.NodeUrls is { Count: > 0 }
				? (IReadOnlyList<string>)options.NodeUrls
				: [options.ElasticsearchUrl];
			return new NodeFailoverHandler(ToNodeUris(nodeUrls));
		});

		// Configure the standard resilience pipeline (keyed by the typed client's name) from the exporter's
		// retry options, so config-bound overrides flow through the DI options system.
		_ = services.AddOptions<HttpStandardResilienceOptions>(httpClientBuilder.Name)
			.Configure<IOptions<ElasticsearchExporterOptions>>(static (resilience, exporterOptions) =>
				ConfigureResilience(resilience, exporterOptions.Value.MaxRetryAttempts,
					exporterOptions.Value.RetryBaseDelay, exporterOptions.Value.Timeout));

		_ = services.AddSingleton<IAuditLogExporter, ElasticsearchAuditExporter>();
	}

	private static void AddElasticsearchAuditSinkCore(IServiceCollection services)
	{
		_ = services.AddSingleton<IValidateOptions<ElasticsearchAuditSinkOptions>,
			ElasticsearchAuditSinkOptionsValidator>();

		var httpClientBuilder = services.AddHttpClient<ElasticsearchAuditSink>(static (_, client) =>
		{
			client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
		});

		_ = httpClientBuilder.AddStandardResilienceHandler();

		_ = httpClientBuilder.AddHttpMessageHandler(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchAuditSinkOptions>>().Value;
			return new NodeFailoverHandler(ToNodeUris(options.GetResolvedNodeUrls()));
		});

		_ = services.AddOptions<HttpStandardResilienceOptions>(httpClientBuilder.Name)
			.Configure<IOptions<ElasticsearchAuditSinkOptions>>(static (resilience, sinkOptions) =>
				ConfigureResilience(resilience, sinkOptions.Value.MaxRetryAttempts,
					sinkOptions.Value.RetryBaseDelay, sinkOptions.Value.Timeout));

		_ = services.AddSingleton<ElasticsearchAuditSink>();
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
