// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

/// <summary>
/// Internal implementation of the SQL Server saga builder.
/// </summary>
/// <remarks>
/// Connection overloads use <b>last-wins</b> semantics: each connection method
/// clears any previously configured connection state.
/// </remarks>
internal sealed class SqlServerSagaBuilder : ISqlServerSagaBuilder
{
	private readonly SqlServerSagaStoreOptions _options;

	internal SqlServerSagaBuilder(SqlServerSagaStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, Func<SqlConnection>>? ConnectionFactoryFunc { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	// --- Connection overloads (last-wins) ---

	/// <inheritdoc/>
	public ISqlServerSagaBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerSagaBuilder ConnectionFactory(
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		ConnectionFactoryFunc = connectionFactory;
		_options.ConnectionString = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerSagaBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerSagaBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	// --- Feature-specific ---

	/// <inheritdoc/>
	public ISqlServerSagaBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);

		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerSagaBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		_options.TableName = tableName;
		return this;
	}
}
