// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.Outbox;

namespace Excalibur.Data.Postgres;

/// <summary>
/// Internal implementation of the Postgres outbox builder.
/// </summary>
internal sealed class PostgresOutboxBuilder : IPostgresOutboxBuilder
{
	private readonly PostgresOutboxStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresOutboxBuilder"/> class.
	/// </summary>
	/// <param name="options">The Postgres outbox options to configure.</param>
	public PostgresOutboxBuilder(PostgresOutboxStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc/>
	public IPostgresOutboxBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresOutboxBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.OutboxTableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresOutboxBuilder DeadLetterTableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.DeadLetterTableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresOutboxBuilder CommandTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Command timeout must be positive.");
		}

		// Note: PostgresOutboxStoreOptions uses BatchProcessingTimeout from base class
		// for command-level timeout configuration
		_options.BatchProcessingTimeout = timeout;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresOutboxBuilder ReservationTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Reservation timeout must be positive.");
		}

		_options.ReservationTimeout = (int)timeout.TotalSeconds;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresOutboxBuilder MaxAttempts(int maxAttempts)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAttempts, 0);
		_options.MaxAttempts = maxAttempts;
		return this;
	}
}
