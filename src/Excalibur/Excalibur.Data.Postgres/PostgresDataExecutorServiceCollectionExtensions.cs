// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;


using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Postgres data executors.
/// </summary>
public static class PostgresDataExecutorServiceCollectionExtensions
{
	/// <summary>
	/// Registers Postgres data executors with the specified connection factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionFactory">Factory for creating database connections.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddPostgresDataExecutors(
		this IServiceCollection services,
		Func<IDbConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddTransient(_ => connectionFactory());
		return services;
	}
}
