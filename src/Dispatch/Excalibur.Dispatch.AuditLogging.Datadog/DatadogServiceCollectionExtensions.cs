// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Datadog;
using Excalibur.Dispatch.Compliance;

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
	[RequiresDynamicCode("Validating data annotations requires dynamic code generation.")]
	[RequiresUnreferencedCode("Validating data annotations requires unreferenced members.")]
	public static IServiceCollection AddDatadogAuditExporter(
		this IServiceCollection services,
		Action<DatadogExporterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<DatadogExporterOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddHttpClient<DatadogAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<DatadogExporterOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, DatadogAuditExporter>();

		return services;
	}
}
