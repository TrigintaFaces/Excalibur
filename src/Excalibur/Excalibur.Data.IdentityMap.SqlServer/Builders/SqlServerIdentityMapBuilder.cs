// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.IdentityMap.SqlServer.Builders;

/// <summary>
/// Internal implementation of the SQL Server identity map builder.
/// </summary>
/// <remarks>
/// <para>
/// Connection overloads use <b>last-wins</b> semantics: each connection method
/// clears any previously configured connection state to prevent ambiguity.
/// </para>
/// <para>
/// Feature-specific methods (schema, table, timeout, batch size) are additive
/// and do not clear connection state.
/// </para>
/// </remarks>
internal sealed class SqlServerIdentityMapBuilder : ISqlServerIdentityMapBuilder
{
	private readonly SqlServerIdentityMapOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerIdentityMapBuilder"/> class.
	/// </summary>
	/// <param name="options">The SQL Server identity map options to configure.</param>
	internal SqlServerIdentityMapBuilder(SqlServerIdentityMapOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Gets the connection factory, if configured via <see cref="ConnectionFactory"/>.
	/// </summary>
	internal Func<IServiceProvider, Func<SqlConnection>>? ConnectionFactoryFunc { get; private set; }

	/// <summary>
	/// Gets the connection string name for resolution from IConfiguration.
	/// </summary>
	internal string? ConnectionStringNameValue { get; private set; }

	/// <summary>
	/// Gets the BindConfiguration section path, if set.
	/// </summary>
	internal string? BindConfigurationPath { get; private set; }

	// --- Connection overloads (last-wins: each clears the others) ---

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder ConnectionFactory(
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
	public ISqlServerIdentityMapBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = null;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	// --- Feature-specific configuration ---

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder SchemaName(string schemaName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		_options.SchemaName = schemaName;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.TableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder CommandTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Command timeout must be positive.");
		}

		_options.CommandTimeoutSeconds = (int)timeout.TotalSeconds;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder MaxBatchSize(int maxBatchSize)
	{
		if (maxBatchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxBatchSize), "Max batch size must be positive.");
		}

		_options.MaxBatchSize = maxBatchSize;
		return this;
	}

	internal bool HealthChecksEnabled { get; private set; }
	internal string HealthCheckName { get; private set; } = "sqlserver-identitymap";

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder EnableHealthChecks(string? name = null)
	{
		HealthChecksEnabled = true;
		if (!string.IsNullOrWhiteSpace(name))
		{
			HealthCheckName = name;
		}

		return this;
	}
}
