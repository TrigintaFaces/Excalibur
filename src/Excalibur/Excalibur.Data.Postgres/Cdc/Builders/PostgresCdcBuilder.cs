// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Internal implementation of the Postgres CDC builder.
/// </summary>
internal sealed class PostgresCdcBuilder : IPostgresCdcBuilder
{
	private readonly PostgresCdcOptions _options;
	private readonly PostgresCdcStateStoreOptions _stateStoreOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcBuilder"/> class.
	/// </summary>
	/// <param name="options">The Postgres CDC options to configure.</param>
	/// <param name="stateStoreOptions">The CDC state store options to configure.</param>
	public PostgresCdcBuilder(PostgresCdcOptions options, PostgresCdcStateStoreOptions stateStoreOptions)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_stateStoreOptions = stateStoreOptions ?? throw new ArgumentNullException(nameof(stateStoreOptions));
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder SchemaName(string schema)
	{
		if (string.IsNullOrWhiteSpace(schema))
		{
			throw new ArgumentException("Schema name cannot be null or whitespace.", nameof(schema));
		}

		_stateStoreOptions.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder StateTableName(string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName));
		}

		_stateStoreOptions.TableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder ReplicationSlotName(string slotName)
	{
		if (string.IsNullOrWhiteSpace(slotName))
		{
			throw new ArgumentException("Replication slot name cannot be null or whitespace.", nameof(slotName));
		}

		_options.ReplicationSlotName = slotName;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder PublicationName(string publicationName)
	{
		if (string.IsNullOrWhiteSpace(publicationName))
		{
			throw new ArgumentException("Publication name cannot be null or whitespace.", nameof(publicationName));
		}

		_options.PublicationName = publicationName;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder PollingInterval(TimeSpan interval)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), interval, "Polling interval must be positive.");
		}

		_options.PollingInterval = interval;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder BatchSize(int size)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
		_options.BatchSize = size;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder Timeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
		}

		_options.Timeout = timeout;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder ProcessorId(string processorId)
	{
		if (string.IsNullOrWhiteSpace(processorId))
		{
			throw new ArgumentException("Processor ID cannot be null or whitespace.", nameof(processorId));
		}

		_options.ProcessorId = processorId;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder UseBinaryProtocol(bool useBinary = true)
	{
		_options.UseBinaryProtocol = useBinary;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder AutoCreateSlot(bool autoCreate = true)
	{
		_options.AutoCreateSlot = autoCreate;
		return this;
	}
}
