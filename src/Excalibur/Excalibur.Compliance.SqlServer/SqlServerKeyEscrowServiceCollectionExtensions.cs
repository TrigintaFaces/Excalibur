// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Compliance.SqlServer;
using Excalibur.Dispatch.Compliance;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server key escrow services.
/// </summary>
public static class SqlServerKeyEscrowServiceCollectionExtensions
{
	/// <summary>
	/// Adds the SQL Server key escrow service to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">A delegate to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerKeyEscrow(
		this IServiceCollection services,
		Action<SqlServerKeyEscrowOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<SqlServerKeyEscrowOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerKeyEscrowOptions>,
				SqlServerKeyEscrowOptionsValidator>());
		services.TryAddSingleton<IKeyEscrowService, SqlServerKeyEscrowService>();

		return services;
	}

	/// <summary>
	/// Adds the SQL Server key escrow service to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerKeyEscrow(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<SqlServerKeyEscrowOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<SqlServerKeyEscrowOptions>,
				SqlServerKeyEscrowOptionsValidator>());
		services.TryAddSingleton<IKeyEscrowService, SqlServerKeyEscrowService>();

		return services;
	}

}
