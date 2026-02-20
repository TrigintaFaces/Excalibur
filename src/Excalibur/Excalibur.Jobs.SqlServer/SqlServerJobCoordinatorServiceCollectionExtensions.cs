// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Coordination;
using Excalibur.Jobs.SqlServer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring SQL Server-based job coordination services.
/// </summary>
public static class SqlServerJobCoordinatorServiceCollectionExtensions
{
	/// <summary>
	/// Adds distributed job coordination services using SQL Server as the coordination backend.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <param name="configure">The configuration action for SQL Server job coordinator options.</param>
	/// <returns>The configured <see cref="IServiceCollection"/>.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	public static IServiceCollection AddSqlServerJobCoordinator(
		this IServiceCollection services,
		Action<SqlServerJobCoordinatorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SqlServerJobCoordinatorOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		_ = services.AddSingleton<SqlServerJobCoordinator>();
		_ = services.AddSingleton<IJobCoordinator>(sp => sp.GetRequiredService<SqlServerJobCoordinator>());
		_ = services.AddSingleton<IJobLockProvider>(sp => sp.GetRequiredService<SqlServerJobCoordinator>());
		_ = services.AddSingleton<IJobRegistry>(sp => sp.GetRequiredService<SqlServerJobCoordinator>());
		_ = services.AddSingleton<IJobDistributor>(sp => sp.GetRequiredService<SqlServerJobCoordinator>());

		return services;
	}
}
