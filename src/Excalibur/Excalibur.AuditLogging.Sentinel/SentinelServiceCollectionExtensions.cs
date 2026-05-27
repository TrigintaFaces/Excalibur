// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.AuditLogging.Sentinel;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;
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

		_ = services.AddHttpClient<SentinelAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<SentinelExporterOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, SentinelAuditExporter>();
	}
}
