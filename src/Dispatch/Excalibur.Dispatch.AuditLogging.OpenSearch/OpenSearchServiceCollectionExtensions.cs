// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.OpenSearch;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
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
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
    [RequiresDynamicCode("Validating data annotations requires dynamic code generation.")]
    [RequiresUnreferencedCode("Validating data annotations requires unreferenced members.")]
    public static IServiceCollection AddOpenSearchAuditExporter(
        this IServiceCollection services,
        Action<OpenSearchExporterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        _ = services.AddOptions<OpenSearchExporterOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddOpenSearchAuditExporterCore();
    }

    /// <summary>
    /// Adds OpenSearch audit log exporter services using an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind to <see cref="OpenSearchExporterOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
    [RequiresDynamicCode("Validating data annotations requires dynamic code generation.")]
    [RequiresUnreferencedCode("Validating data annotations requires unreferenced members.")]
    public static IServiceCollection AddOpenSearchAuditExporter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.AddOptions<OpenSearchExporterOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services.AddOpenSearchAuditExporterCore();
    }

    /// <summary>
    /// Adds the OpenSearch audit sink for real-time audit event indexing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenSearchAuditSink(
        this IServiceCollection services,
        Action<OpenSearchAuditSinkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        _ = services.AddOptions<OpenSearchAuditSinkOptions>()
            .Configure(configure)
            .ValidateOnStart();

        return services.AddOpenSearchAuditSinkCore();
    }

    /// <summary>
    /// Adds the OpenSearch audit sink using an <see cref="IConfiguration"/> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind to <see cref="OpenSearchAuditSinkOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenSearchAuditSink(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

#pragma warning disable IL2026, IL3050 // Bind and ValidateDataAnnotations require unreferenced/dynamic code
        _ = services.AddOptions<OpenSearchAuditSinkOptions>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();
#pragma warning restore IL2026, IL3050

        return services.AddOpenSearchAuditSinkCore();
    }

    private static IServiceCollection AddOpenSearchAuditExporterCore(this IServiceCollection services)
    {
        _ = services.AddSingleton<IValidateOptions<OpenSearchExporterOptions>,
            OpenSearchExporterOptionsValidator>();

        _ = services.AddHttpClient<OpenSearchAuditExporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenSearchExporterOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<IAuditLogExporter, OpenSearchAuditExporter>();

        return services;
    }

    private static IServiceCollection AddOpenSearchAuditSinkCore(this IServiceCollection services)
    {
        _ = services.AddSingleton<IValidateOptions<OpenSearchAuditSinkOptions>,
            OpenSearchAuditSinkOptionsValidator>();

        _ = services.AddHttpClient<OpenSearchAuditSink>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenSearchAuditSinkOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<OpenSearchAuditSink>();

        return services;
    }
}
