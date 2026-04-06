// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.GoogleCloud;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Microsoft.Extensions.Configuration;
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
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	public static IServiceCollection AddGoogleCloudAuditExporter(
		this IServiceCollection services,
		Action<GoogleCloudAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<GoogleCloudAuditOptions>()
			.Configure(configure)
			.ValidateOnStart();

		RegisterGoogleCloudAuditExporterCore(services);

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Logging audit log exporter services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="GoogleCloudAuditOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddGoogleCloudAuditExporter(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<GoogleCloudAuditOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		RegisterGoogleCloudAuditExporterCore(services);

		return services;
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
