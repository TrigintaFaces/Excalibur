// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.AuditLogging.Postgres;

/// <summary>
/// Internal implementation of the Postgres audit logging builder.
/// Connection overloads use last-wins semantics.
/// </summary>
internal sealed class PostgresAuditLoggingBuilder : IPostgresAuditLoggingBuilder
{
	private readonly PostgresAuditOptions _options;

	internal PostgresAuditLoggingBuilder(PostgresAuditOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, NpgsqlDataSource>? DataSourceFactoryFunc { get; private set; }
	internal NpgsqlDataSource? DataSourceInstance { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	public IPostgresAuditLoggingBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresAuditLoggingBuilder DataSourceFactory(Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory)
	{
		ArgumentNullException.ThrowIfNull(dataSourceFactory);
		DataSourceFactoryFunc = dataSourceFactory;
		_options.ConnectionString = null!;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresAuditLoggingBuilder DataSource(NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		DataSourceInstance = dataSource;
		_options.ConnectionString = null!;
		DataSourceFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresAuditLoggingBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ConnectionStringNameValue = name;
		_options.ConnectionString = null!;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	public IPostgresAuditLoggingBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null!;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		return this;
	}

	public IPostgresAuditLoggingBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		_options.SchemaName = schema;
		return this;
	}

	public IPostgresAuditLoggingBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.TableName = tableName;
		return this;
	}
}
