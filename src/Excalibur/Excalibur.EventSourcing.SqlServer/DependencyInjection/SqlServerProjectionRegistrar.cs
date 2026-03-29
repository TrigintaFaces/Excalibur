// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.SqlServer.DependencyInjection;

/// <summary>
/// Registrar for adding multiple SQL Server projection stores that share
/// a common connection string. Used with
/// <c>AddSqlServerProjections</c>.
/// </summary>
/// <remarks>
/// Each projection type gets its own options instance, so per-projection
/// overrides (table name, JSON options, etc.) are fully isolated.
/// </remarks>
public sealed class SqlServerProjectionRegistrar
{
	private readonly IServiceCollection _services;
	private readonly string? _connectionString;
	private readonly Action<SqlServerProjectionStoreOptions>? _configureShared;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerProjectionRegistrar"/> class
	/// with an explicit connection string.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="connectionString">The shared SQL Server connection string.</param>
	internal SqlServerProjectionRegistrar(IServiceCollection services, string connectionString)
	{
		_services = services;
		_connectionString = connectionString;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerProjectionRegistrar"/> class
	/// with a shared options configuration action.
	/// </summary>
	internal SqlServerProjectionRegistrar(IServiceCollection services, Action<SqlServerProjectionStoreOptions> configureShared)
	{
		_services = services;
		_configureShared = configureShared;
	}

	/// <summary>
	/// Adds a projection store for the specified type using the shared configuration.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="configureOptions">Optional action to override per-projection options (e.g., table name).</param>
	/// <returns>This registrar for fluent chaining.</returns>
	public SqlServerProjectionRegistrar Add<TProjection>(
		Action<SqlServerProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		_services.AddSqlServerProjectionStore<TProjection>(options =>
		{
			if (_configureShared != null)
			{
				_configureShared(options);
			}
			else
			{
				options.ConnectionString = _connectionString!;
			}

			configureOptions?.Invoke(options);
		});

		return this;
	}
}
