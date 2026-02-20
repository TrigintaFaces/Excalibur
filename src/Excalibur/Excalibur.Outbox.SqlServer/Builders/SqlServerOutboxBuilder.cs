// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Internal implementation of the SQL Server outbox builder.
/// </summary>
internal sealed class SqlServerOutboxBuilder : ISqlServerOutboxBuilder
{
	private readonly SqlServerOutboxOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerOutboxBuilder"/> class.
	/// </summary>
	/// <param name="options">The SQL Server outbox options to configure.</param>
	public SqlServerOutboxBuilder(SqlServerOutboxOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

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

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder CommandTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Command timeout must be positive.");
		}

		_options.CommandTimeoutSeconds = (int)timeout.TotalSeconds;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder UseRowLocking(bool enable = true)
	{
		_options.UseRowLocking = enable;
		return this;
	}

	/// <inheritdoc/>
	public ISqlServerOutboxBuilder DefaultBatchSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
		_options.DefaultBatchSize = size;
		return this;
	}
}
