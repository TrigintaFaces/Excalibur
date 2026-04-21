// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Internal implementation of the Postgres leader election builder.
/// Connection overloads use last-wins semantics.
/// </summary>
internal sealed class PostgresLeaderElectionBuilder : IPostgresLeaderElectionBuilder
{
	private readonly PostgresLeaderElectionOptions _options;

	internal PostgresLeaderElectionBuilder(PostgresLeaderElectionOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, NpgsqlDataSource>? DataSourceFactoryFunc { get; private set; }
	internal NpgsqlDataSource? DataSourceInstance { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IPostgresLeaderElectionBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresLeaderElectionBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory)
	{
		ArgumentNullException.ThrowIfNull(dataSourceFactory);

		DataSourceFactoryFunc = dataSourceFactory;
		_options.ConnectionString = null!;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresLeaderElectionBuilder DataSource(NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(dataSource);

		DataSourceInstance = dataSource;
		_options.ConnectionString = null!;
		DataSourceFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresLeaderElectionBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = null!;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresLeaderElectionBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null!;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		return this;
	}

	public IPostgresLeaderElectionBuilder LockKey(long lockKey)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(lockKey, 1);

		_options.LockKey = lockKey;
		return this;
	}
}
