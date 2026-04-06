// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Datadog;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
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
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	public static IServiceCollection AddDatadogAuditExporter(
		this IServiceCollection services,
		Action<DatadogExporterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DatadogExporterOptions>()
			.Configure(configure)
			.ValidateOnStart();

		RegisterDatadogAuditExporterCore(services);

		return services;
	}

	/// <summary>
	/// Adds Datadog audit log exporter services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="DatadogExporterOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDatadogAuditExporter(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<DatadogExporterOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		RegisterDatadogAuditExporterCore(services);

		return services;
	}

	private static void RegisterDatadogAuditExporterCore(IServiceCollection services)
	{
		_ = services.AddHttpClient<DatadogAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<DatadogExporterOptions>>().Value;
			client.Timeout = options.Retry.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, DatadogAuditExporter>();
	}
}
