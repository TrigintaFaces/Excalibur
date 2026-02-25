// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Splunk;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Splunk audit exporter services.
/// </summary>
public static class SplunkServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Splunk audit log exporter to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure the Splunk exporter options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddSplunkAuditExporter(
		this IServiceCollection services,
		Action<SplunkExporterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.Configure(configure);

		_ = services.AddHttpClient<SplunkAuditExporter>((sp, client) =>
			{
				var options = sp.GetRequiredService<Options.IOptions<SplunkExporterOptions>>().Value;
				client.Timeout = options.RequestTimeout;
			})
			.ConfigurePrimaryHttpMessageHandler(sp =>
			{
				var options = sp.GetRequiredService<Options.IOptions<SplunkExporterOptions>>().Value;

				var handler = new HttpClientHandler();

				if (!options.ValidateCertificate)
				{
					handler.ServerCertificateCustomValidationCallback =
						HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
				}

				return handler;
			});

		services.TryAddSingleton<IAuditLogExporter, SplunkAuditExporter>();

		return services;
	}

	/// <summary>
	/// Adds the Splunk audit log exporter to the service collection with options from configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configurationSection"> The configuration section name containing Splunk options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresDynamicCode("Binding configuration and validating data annotations require dynamic code generation.")]
	[RequiresUnreferencedCode("Binding configuration and validating data annotations require unreferenced members.")]
	public static IServiceCollection AddSplunkAuditExporter(
		this IServiceCollection services,
		string configurationSection = "Splunk")
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddOptions<SplunkExporterOptions>()
			.BindConfiguration(configurationSection)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddHttpClient<SplunkAuditExporter>((sp, client) =>
			{
				var options = sp.GetRequiredService<Options.IOptions<SplunkExporterOptions>>().Value;
				client.Timeout = options.RequestTimeout;
			})
			.ConfigurePrimaryHttpMessageHandler(sp =>
			{
				var options = sp.GetRequiredService<Options.IOptions<SplunkExporterOptions>>().Value;

				var handler = new HttpClientHandler();

				if (!options.ValidateCertificate)
				{
					handler.ServerCertificateCustomValidationCallback =
						HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
				}

				return handler;
			});

		services.TryAddSingleton<IAuditLogExporter, SplunkAuditExporter>();

		return services;
	}
}
