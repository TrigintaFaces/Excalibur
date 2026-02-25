// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.AuditLogging.GoogleCloud;
using Excalibur.Dispatch.Compliance;

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
	[RequiresDynamicCode("Validating data annotations requires dynamic code generation.")]
	[RequiresUnreferencedCode("Validating data annotations requires unreferenced members.")]
	public static IServiceCollection AddGoogleCloudAuditExporter(
		this IServiceCollection services,
		Action<GoogleCloudAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<GoogleCloudAuditOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddHttpClient<GoogleCloudLoggingAuditExporter>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<GoogleCloudAuditOptions>>().Value;
			client.Timeout = options.Timeout;
		});

		_ = services.AddSingleton<IAuditLogExporter, GoogleCloudLoggingAuditExporter>();

		return services;
	}
}
