// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap.SqlServer.Builders;

/// <summary>
/// Internal implementation of the SQL Server identity map builder.
/// </summary>
internal sealed class SqlServerIdentityMapBuilder : ISqlServerIdentityMapBuilder
{
	private readonly SqlServerIdentityMapOptions _options;

	public SqlServerIdentityMapBuilder(SqlServerIdentityMapOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public ISqlServerIdentityMapBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		_options.ConnectionString = connectionString;
		return this;
	}

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
}
