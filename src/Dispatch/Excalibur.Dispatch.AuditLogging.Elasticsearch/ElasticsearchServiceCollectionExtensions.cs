// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Elasticsearch;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch audit exporter services.
/// </summary>
public static class ElasticsearchServiceCollectionExtensions
{
	/// <summary>
	/// Adds Elasticsearch audit log exporter services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	[RequiresDynamicCode("Validating data annotations requires dynamic code generation.")]
	[RequiresUnreferencedCode("Validating data annotations requires unreferenced members.")]
	public static IServiceCollection AddElasticsearchAuditExporter(
		this IServiceCollection services,
		Action<ElasticsearchExporterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ElasticsearchExporterOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddHttpClient<ElasticsearchAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchExporterOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, ElasticsearchAuditExporter>();

		return services;
	}
}
