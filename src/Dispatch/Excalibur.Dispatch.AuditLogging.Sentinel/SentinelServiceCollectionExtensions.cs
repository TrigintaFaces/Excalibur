// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Sentinel;
using Excalibur.Dispatch.Compliance;

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
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	[RequiresDynamicCode("Binding configuration and validating data annotations require dynamic code generation.")]
	[RequiresUnreferencedCode("Binding configuration and validating data annotations require unreferenced members.")]
	public static IServiceCollection AddSentinelAuditExporter(
		this IServiceCollection services,
		Action<SentinelExporterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SentinelExporterOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddHttpClient<SentinelAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<SentinelExporterOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, SentinelAuditExporter>();

		return services;
	}
}
