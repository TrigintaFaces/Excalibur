// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle;

/// <summary>
/// Internal implementation of the Oracle saga builder.
/// </summary>
/// <remarks>
/// Connection overloads use <b>last-wins</b> semantics: each connection method clears any previously
/// configured connection state.
/// </remarks>
internal sealed class OracleSagaBuilder : IOracleSagaBuilder
{
	private readonly OracleSagaStoreOptions _options;

	internal OracleSagaBuilder(OracleSagaStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, Func<OracleConnection>>? ConnectionFactoryFunc { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	public IOracleSagaBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleSagaBuilder ConnectionFactory(
		Func<IServiceProvider, Func<OracleConnection>> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		ConnectionFactoryFunc = connectionFactory;
		_options.ConnectionString = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleSagaBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleSagaBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleSagaBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);

		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public IOracleSagaBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		_options.TableName = tableName;
		return this;
	}
}
