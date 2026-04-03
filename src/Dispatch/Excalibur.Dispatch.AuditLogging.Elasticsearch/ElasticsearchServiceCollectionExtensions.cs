// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.Elasticsearch;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// Note: IAuditStore is NOT registered from this package.
// Elasticsearch serves as a search/analytics sink, not a compliance-grade audit store.
// See ADR-290 for rationale.

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch audit services.
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

		return services.AddElasticsearchAuditExporterCore();
	}

	/// <summary>
	/// Adds Elasticsearch audit log exporter services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ElasticsearchExporterOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
	[RequiresDynamicCode("Validating data annotations requires dynamic code generation.")]
	[RequiresUnreferencedCode("Validating data annotations requires unreferenced members.")]
	public static IServiceCollection AddElasticsearchAuditExporter(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ElasticsearchExporterOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services.AddElasticsearchAuditExporterCore();
	}

	/// <summary>
	/// Adds the Elasticsearch audit sink for real-time audit event indexing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticsearchAuditSink(
		this IServiceCollection services,
		Action<ElasticsearchAuditSinkOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ElasticsearchAuditSinkOptions>()
			.Configure(configure)
			.ValidateOnStart();

		return services.AddElasticsearchAuditSinkCore();
	}

	/// <summary>
	/// Adds the Elasticsearch audit sink using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="ElasticsearchAuditSinkOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticsearchAuditSink(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ElasticsearchAuditSinkOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return services.AddElasticsearchAuditSinkCore();
	}

	private static IServiceCollection AddElasticsearchAuditExporterCore(this IServiceCollection services)
	{
		_ = services.AddSingleton<IValidateOptions<ElasticsearchExporterOptions>,
			ElasticsearchExporterOptionsValidator>();

		_ = services.AddHttpClient<ElasticsearchAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchExporterOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, ElasticsearchAuditExporter>();

		return services;
	}

	private static IServiceCollection AddElasticsearchAuditSinkCore(this IServiceCollection services)
	{
		_ = services.AddSingleton<IValidateOptions<ElasticsearchAuditSinkOptions>,
			ElasticsearchAuditSinkOptionsValidator>();

		_ = services.AddHttpClient<ElasticsearchAuditSink>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchAuditSinkOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<ElasticsearchAuditSink>();

		return services;
	}

}
