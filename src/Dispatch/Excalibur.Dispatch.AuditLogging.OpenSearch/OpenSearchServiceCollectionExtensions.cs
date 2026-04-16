// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.OpenSearch;
using Excalibur.Dispatch.Compliance;

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

        _ = services.AddHttpClient<OpenSearchAuditExporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenSearchExporterOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<IAuditLogExporter, OpenSearchAuditExporter>();
    }

    private static void AddOpenSearchAuditSinkCore(IServiceCollection services)
    {
        _ = services.AddSingleton<IValidateOptions<OpenSearchAuditSinkOptions>,
            OpenSearchAuditSinkOptionsValidator>();

        _ = services.AddHttpClient<OpenSearchAuditSink>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenSearchAuditSinkOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<OpenSearchAuditSink>();
    }
}
