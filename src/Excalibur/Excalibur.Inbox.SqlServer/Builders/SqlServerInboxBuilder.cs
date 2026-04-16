// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Inbox.SqlServer;

/// <summary>
/// Internal implementation of the SQL Server inbox builder.
/// </summary>
/// <remarks>
/// Connection overloads use <b>last-wins</b> semantics: each connection method
/// clears any previously configured connection state.
/// </remarks>
internal sealed class SqlServerInboxBuilder : ISqlServerInboxBuilder
{
	private readonly SqlServerInboxOptions _options;

	internal SqlServerInboxBuilder(SqlServerInboxOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, Func<SqlConnection>>? ConnectionFactoryFunc { get; private set; }
	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }
	internal TimeSpan? DeduplicationWindowValue { get; private set; }

	// --- Connection overloads (last-wins) ---

	/// <inheritdoc/>
	public ISqlServerInboxBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerInboxBuilder ConnectionFactory(
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
	public ISqlServerInboxBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = string.Empty;
		ConnectionFactoryFunc = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerInboxBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		_options.ConnectionString = string.Empty;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	// --- Feature-specific ---

	/// <inheritdoc/>
	public ISqlServerInboxBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);

		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerInboxBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		_options.TableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerInboxBuilder DeduplicationWindow(TimeSpan window)
	{
		if (window <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(window), window, "Deduplication window must be positive.");
		}

		DeduplicationWindowValue = window;
		return this;
	}

	internal bool HealthChecksEnabled { get; private set; }
	internal string HealthCheckName { get; private set; } = "sqlserver-inbox";

	/// <inheritdoc/>
	public ISqlServerInboxBuilder EnableHealthChecks(string? name = null)
	{
		HealthChecksEnabled = true;
		if (!string.IsNullOrWhiteSpace(name))
		{
			HealthCheckName = name;
		}

		return this;
	}
}
