// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions.Execution;
using Excalibur.Data.SqlServer.ErrorHandling;
using Excalibur.Data.SqlServer.Execution;
using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SQL Server data services.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers provider-neutral data execution/query interfaces with SQL Server implementations. Caller supplies an IDbConnection factory
	/// (scoped/transient) via DI.
	/// </summary>
	public static IServiceCollection AddSqlServerDataExecutors(
		this IServiceCollection services,
		Func<IDbConnection> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		services.TryAddTransient(_ => connectionFactory());
		services.TryAddTransient<IDataExecutor, SqlServerDataExecutor>();
		services.TryAddTransient<IQueryExecutor, SqlServerQueryExecutor>();
		return services;
	}

	/// <summary>
	/// Registers the SQL Server dead letter store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerDeadLetterStore(
		this IServiceCollection services,
		string connectionString)
	{
		return services.AddSqlServerDeadLetterStore(options => options.ConnectionString = connectionString);
	}

	/// <summary>
	/// Registers the SQL Server dead letter store with full configuration options.
	/// Uses IOptions pattern for configuration consistency with other SqlServer stores.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the dead letter options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSqlServerDeadLetterStore(
		this IServiceCollection services,
		Action<SqlServerDeadLetterOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register options via IOptions pattern
		_ = services.Configure(configure);

		// Validate options at startup
		var options = new SqlServerDeadLetterOptions();
		configure(options);

		if (string.IsNullOrWhiteSpace(options.ConnectionString))
		{
			throw new ArgumentException("ConnectionString must be provided", nameof(configure));
		}

		// Register dead letter store (uses IOptions pattern via DI)
		services.TryAddSingleton<SqlServerDeadLetterStore>();
		services.TryAddSingleton<IDeadLetterStore>(sp => sp.GetRequiredService<SqlServerDeadLetterStore>());

		return services;
	}
}
