// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Aws;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS CloudWatch audit exporter services.
/// </summary>
public static class AwsAuditServiceCollectionExtensions
{
    /// <summary>
    /// Adds AWS CloudWatch audit log exporter services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for the AWS audit builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
    /// <example>
    /// <code>
    /// services.AddAwsAuditExporter(aws =&gt;
    /// {
    ///     aws.LogGroupName("/dispatch/audit")
    ///        .Region("us-east-1")
    ///        .StreamName("my-stream");
    /// });
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
        Justification = "Options validation/binding uses reflection by design.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Configuration binding uses reflection by design.")]
    public static IServiceCollection AddAwsAuditExporter(
        this IServiceCollection services,
        Action<IAuditLoggingAwsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AwsAuditOptions
        {
            LogGroupName = null!,
            Region = null!,
        };
        var builder = new AuditLoggingAwsBuilder(options);
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
        AuditLoggingAwsBuilder builder,
        AwsAuditOptions options)
    {
        _ = services.Configure<AwsAuditOptions>(opt =>
        {
            opt.LogGroupName = options.LogGroupName;
            opt.Region = options.Region;
            opt.StreamName = options.StreamName;
            opt.ServiceUrl = options.ServiceUrl;
            opt.BatchSize = options.BatchSize;
            opt.MaxRetryAttempts = options.MaxRetryAttempts;
            opt.RetryBaseDelay = options.RetryBaseDelay;
            opt.Timeout = options.Timeout;
        });

        if (builder.BindConfigurationPath is not null)
        {
            _ = services.AddOptions<AwsAuditOptions>()
                .BindConfiguration(builder.BindConfigurationPath)
                .ValidateOnStart();
        }

        _ = services.AddOptions<AwsAuditOptions>().ValidateOnStart();

        RegisterAwsAuditExporterCore(services);
    }

    private static void RegisterAwsAuditExporterCore(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<AwsAuditOptions>, AwsAuditOptionsValidator>());

        _ = services.AddHttpClient<AwsCloudWatchAuditExporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AwsAuditOptions>>().Value;
            client.Timeout = options.Timeout;
        });

        _ = services.AddSingleton<IAuditLogExporter, AwsCloudWatchAuditExporter>();
    }
}
