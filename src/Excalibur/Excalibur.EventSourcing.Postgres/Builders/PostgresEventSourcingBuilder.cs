// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Postgres.DependencyInjection;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Internal implementation of the Postgres event sourcing builder.
/// </summary>
/// <remarks>
/// <para>
/// Connection overloads use <b>last-wins</b> semantics: each connection method
/// clears any previously configured connection state. All connection paths
/// converge to <see cref="NpgsqlDataSource"/> for proper connection pooling.
/// </para>
/// </remarks>
internal sealed class PostgresEventSourcingBuilder : IPostgresEventSourcingBuilder
{
	private readonly PostgresEventSourcingOptions _options;

	internal PostgresEventSourcingBuilder(PostgresEventSourcingOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, NpgsqlDataSource>? DataSourceFactoryFunc { get; private set; }
	internal NpgsqlDataSource? DataSourceInstance { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	// --- Connection overloads (last-wins: each clears the others) ---

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder DataSourceFactory(
		Func<IServiceProvider, NpgsqlDataSource> dataSourceFactory)
	{
		ArgumentNullException.ThrowIfNull(dataSourceFactory);

		DataSourceFactoryFunc = dataSourceFactory;
		_options.ConnectionString = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder DataSource(NpgsqlDataSource dataSource)
	{
		ArgumentNullException.ThrowIfNull(dataSource);

		DataSourceInstance = dataSource;
		_options.ConnectionString = null;
		DataSourceFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = null;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null;
		DataSourceFactoryFunc = null;
		DataSourceInstance = null;
		ConnectionStringNameValue = null;
		return this;
	}

	// --- Feature-specific configuration ---

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder EventStoreSchema(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);

		_options.EventStoreSchema = schema;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder EventStoreTable(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		_options.EventStoreTable = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder SnapshotStoreSchema(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);

		_options.SnapshotStoreSchema = schema;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresEventSourcingBuilder SnapshotStoreTable(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		_options.SnapshotStoreTable = tableName;
		return this;
	}
}
