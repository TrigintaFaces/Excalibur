// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Internal implementation of the SQL Server outbox builder.
/// </summary>
/// <remarks>
/// <para>
/// Connection overloads use <b>last-wins</b> semantics: each connection method
/// clears any previously configured connection state to prevent ambiguity.
/// </para>
/// <para>
/// Feature-specific methods (schema/table names) are additive and do not
/// clear connection state.
/// </para>
/// </remarks>
internal sealed class SqlServerOutboxBuilder : ISqlServerOutboxBuilder
{
	private readonly SqlServerOutboxOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxBuilder"/> class.
	/// </summary>
	/// <param name="options">The SQL Server outbox options to configure.</param>
	internal SqlServerOutboxBuilder(SqlServerOutboxOptions options)
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
	public ISqlServerOutboxBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder ConnectionFactory(
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		ConnectionFactoryFunc = connectionFactory;
		_options.ConnectionString = string.Empty;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = string.Empty;
		ConnectionFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = string.Empty;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	// --- Feature-specific configuration ---

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.OutboxTableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder TransportsTableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.TransportsTableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder DeadLetterTableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.DeadLetterTableName = tableName;
		return this;
	}

	/// <summary>Sets the command timeout for SQL operations.</summary>
	public ISqlServerOutboxBuilder CommandTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Command timeout must be positive.");
		}

		_options.CommandTimeoutSeconds = (int)timeout.TotalSeconds;
		return this;
	}

	/// <summary>Enables or disables row-level locking for concurrent access.</summary>
	public ISqlServerOutboxBuilder UseRowLocking(bool enable = true)
	{
		_options.UseRowLocking = enable;
		return this;
	}

	/// <summary>Sets the default batch size for retrieving messages.</summary>
	public ISqlServerOutboxBuilder DefaultBatchSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
		_options.DefaultBatchSize = size;
		return this;
	}

	internal bool HealthChecksEnabled { get; private set; }
	internal string HealthCheckName { get; private set; } = "sqlserver-outbox";

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder EnableHealthChecks(string? name = null)
	{
		HealthChecksEnabled = true;
		if (!string.IsNullOrWhiteSpace(name))
		{
			HealthCheckName = name;
		}

		return this;
	}
}
