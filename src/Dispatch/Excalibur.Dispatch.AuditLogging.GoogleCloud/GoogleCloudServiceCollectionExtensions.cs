// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.GoogleCloud;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Google Cloud Logging audit exporter services.
/// </summary>
public static class GoogleCloudServiceCollectionExtensions
{
    /// <summary>
    /// Adds Google Cloud Logging audit log exporter services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for the Google Cloud audit builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
    /// <example>
    /// <code>
    /// services.AddGoogleCloudAuditExporter(gcp =&gt;
    /// {
    ///     gcp.ProjectId("my-project")
    ///        .LogName("dispatch-audit");
    /// });
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "Options validation/binding uses reflection by design.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Configuration binding uses reflection by design.")]
    public static IServiceCollection AddGoogleCloudAuditExporter(
        this IServiceCollection services,
        Action<IAuditLoggingGoogleCloudBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GoogleCloudAuditOptions
        {
            ProjectId = null!,
        };
        var builder = new AuditLoggingGoogleCloudBuilder(options);
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
        AuditLoggingGoogleCloudBuilder builder,
        GoogleCloudAuditOptions options)
    {
        _ = services.Configure<GoogleCloudAuditOptions>(opt =>
        {
            opt.ProjectId = options.ProjectId;
            opt.LogName = options.LogName;
            opt.ResourceType = options.ResourceType;
            opt.Labels = options.Labels;
            opt.MaxBatchSize = options.MaxBatchSize;
            opt.MaxRetryAttempts = options.MaxRetryAttempts;
            opt.RetryBaseDelay = options.RetryBaseDelay;
            opt.Timeout = options.Timeout;
        });

        if (builder.BindConfigurationPath is not null)
        {
            _ = services.AddOptions<GoogleCloudAuditOptions>()
                .BindConfiguration(builder.BindConfigurationPath)
                .ValidateOnStart();
        }

        _ = services.AddOptions<GoogleCloudAuditOptions>().ValidateOnStart();

        RegisterGoogleCloudAuditExporterCore(services);
    }

    private static void RegisterGoogleCloudAuditExporterCore(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<GoogleCloudAuditOptions>, GoogleCloudAuditOptionsValidator>());

        _ = services.AddHttpClient<GoogleCloudLoggingAuditExporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GoogleCloudAuditOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<IAuditLogExporter, GoogleCloudLoggingAuditExporter>();
    }
}
