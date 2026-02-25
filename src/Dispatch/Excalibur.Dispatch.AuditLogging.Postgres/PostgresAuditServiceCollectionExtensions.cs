// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging.Postgres;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres audit logging services.
/// </summary>
public static class PostgresAuditServiceCollectionExtensions
{
	/// <summary>
	/// Adds Postgres audit logging services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">An action to configure the Postgres audit options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	public static IServiceCollection AddPostgresAuditStore(
		this IServiceCollection services,
		Action<PostgresAuditOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<PostgresAuditOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<PostgresAuditStore>();
		services.TryAddSingleton<IAuditStore>(sp => sp.GetRequiredService<PostgresAuditStore>());

		return services;
	}
}
