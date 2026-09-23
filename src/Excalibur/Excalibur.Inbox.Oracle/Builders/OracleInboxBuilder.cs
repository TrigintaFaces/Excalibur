// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Inbox.Oracle;

/// <summary>
/// Internal implementation of the Oracle inbox builder.
/// </summary>
/// <remarks>
/// Connection overloads use <b>last-wins</b> semantics: each connection method clears any
/// previously configured connection state.
/// </remarks>
internal sealed class OracleInboxBuilder : IOracleInboxBuilder
{
	private readonly OracleInboxOptions _options;

	internal OracleInboxBuilder(OracleInboxOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	internal Func<IServiceProvider, Func<OracleConnection>>? ConnectionFactoryFunc { get; private set; }

	internal string? ConnectionStringNameValue { get; private set; }

	// --- Connection overloads (last-wins) ---

	/// <inheritdoc/>
	public IOracleInboxBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		_options.ConnectionString = connectionString;
		ConnectionFactoryFunc = null;
		ConnectionStringNameValue = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleInboxBuilder ConnectionFactory(
		Func<IServiceProvider, Func<OracleConnection>> connectionFactory)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);

		ConnectionFactoryFunc = connectionFactory;
		_options.ConnectionString = string.Empty;
		ConnectionStringNameValue = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleInboxBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		ConnectionStringNameValue = name;
		_options.ConnectionString = string.Empty;
		ConnectionFactoryFunc = null;
		return this;
	}

	// --- Feature-specific ---

	/// <inheritdoc/>
	public IOracleInboxBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);

		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public IOracleInboxBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		_options.TableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IOracleInboxBuilder CommandTimeoutSeconds(int seconds)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(seconds, 1);

		_options.CommandTimeoutSeconds = seconds;
		return this;
	}

	/// <inheritdoc/>
	public IOracleInboxBuilder MaxRetryCount(int maxRetryCount)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(maxRetryCount);

		_options.MaxRetryCount = maxRetryCount;
		return this;
	}
}
