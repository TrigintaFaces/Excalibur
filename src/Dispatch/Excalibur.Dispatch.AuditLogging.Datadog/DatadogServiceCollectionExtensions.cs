// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Datadog;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Datadog audit exporter services.
/// </summary>
public static class DatadogServiceCollectionExtensions
{
    /// <summary>
    /// Adds Datadog audit log exporter services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for the Datadog audit builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
    /// <example>
    /// <code>
    /// services.AddDatadogAuditExporter(dd =&gt;
    /// {
    ///     dd.ApiKey("your-api-key")
    ///       .Site("datadoghq.com");
    /// });
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "Options validation/binding uses reflection by design.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Configuration binding uses reflection by design.")]
    public static IServiceCollection AddDatadogAuditExporter(
        this IServiceCollection services,
        Action<IAuditLoggingDatadogBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DatadogExporterOptions
        {
            ApiKey = null!,
        };
        var builder = new AuditLoggingDatadogBuilder(options);
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
        AuditLoggingDatadogBuilder builder,
        DatadogExporterOptions options)
    {
        _ = services.Configure<DatadogExporterOptions>(opt =>
        {
            opt.ApiKey = options.ApiKey;
            opt.Site = options.Site;
            opt.Service = options.Service;
            opt.Source = options.Source;
            opt.Hostname = options.Hostname;
            opt.Tags = options.Tags;
            opt.MaxBatchSize = options.MaxBatchSize;
            opt.Retry = options.Retry;
            opt.UseCompression = options.UseCompression;
        });

        if (builder.BindConfigurationPath is not null)
        {
            _ = services.AddOptions<DatadogExporterOptions>()
                .BindConfiguration(builder.BindConfigurationPath)
                .ValidateOnStart();
        }

        _ = services.AddOptions<DatadogExporterOptions>().ValidateOnStart();

        RegisterDatadogAuditExporterCore(services);
    }

    private static void RegisterDatadogAuditExporterCore(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<DatadogExporterOptions>, DatadogExporterOptionsValidator>());

        _ = services.AddHttpClient<DatadogAuditExporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DatadogExporterOptions>>().Value;
            client.Timeout = options.Retry.Timeout;
        });

        _ = services.AddSingleton<IAuditLogExporter, DatadogAuditExporter>();
    }
}
