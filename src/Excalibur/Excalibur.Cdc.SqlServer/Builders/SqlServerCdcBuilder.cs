// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Internal implementation of the SQL Server CDC builder.
/// </summary>
internal sealed class SqlServerCdcBuilder : ISqlServerCdcBuilder
{
	private readonly SqlServerCdcOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcBuilder"/> class.
	/// </summary>
	/// <param name="options">The SQL Server CDC options to configure.</param>
	public SqlServerCdcBuilder(SqlServerCdcOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder SchemaName(string schema)
	{
		if (string.IsNullOrWhiteSpace(schema))
		{
			throw new ArgumentException("Schema name cannot be null or whitespace.", nameof(schema));
		}

		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder StateTableName(string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName));
		}

		_options.StateTableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder PollingInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval, "Polling interval must be positive.");
		}

		_options.PollingInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder BatchSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
		_options.BatchSize = size;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder CommandTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Command timeout must be positive.");
		}

		_options.CommandTimeout = timeout;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder DatabaseName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		_options.DatabaseName = name;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder DatabaseConnectionIdentifier(string identifier)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
		_options.DatabaseConnectionIdentifier = identifier;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder StateConnectionIdentifier(string identifier)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
		_options.StateConnectionIdentifier = identifier;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder CaptureInstances(params string[] captureInstances)
	{
		ArgumentNullException.ThrowIfNull(captureInstances);
		_options.CaptureInstances = captureInstances;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerCdcBuilder StopOnMissingTableHandler(bool stop)
	{
		_options.StopOnMissingTableHandler = stop;
		return this;
	}
}
