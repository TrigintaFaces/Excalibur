// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Elasticsearch;
using Excalibur.Compliance;

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

        _ = services.AddHttpClient<ElasticsearchAuditExporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ElasticsearchExporterOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<IAuditLogExporter, ElasticsearchAuditExporter>();
    }

    private static void AddElasticsearchAuditSinkCore(IServiceCollection services)
    {
        _ = services.AddSingleton<IValidateOptions<ElasticsearchAuditSinkOptions>,
            ElasticsearchAuditSinkOptionsValidator>();

        _ = services.AddHttpClient<ElasticsearchAuditSink>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ElasticsearchAuditSinkOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<ElasticsearchAuditSink>();
    }
}
