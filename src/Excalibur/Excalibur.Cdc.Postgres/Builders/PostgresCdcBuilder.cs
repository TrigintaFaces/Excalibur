// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Internal implementation of the Postgres CDC builder.
/// </summary>
internal sealed class PostgresCdcBuilder : IPostgresCdcBuilder
{
	private readonly PostgresCdcOptions _options;
	private readonly PostgresCdcStateStoreOptions _stateStoreOptions;

	public PostgresCdcBuilder(PostgresCdcOptions options, PostgresCdcStateStoreOptions stateStoreOptions)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_stateStoreOptions = stateStoreOptions ?? throw new ArgumentNullException(nameof(stateStoreOptions));
	}

	/// <summary>
	/// Gets the state connection string, if configured via <see cref="WithStateStore(string)"/>.
	/// </summary>
	internal string? StateConnectionString { get; private set; }

	/// <summary>
	/// Gets the state connection factory, if configured via <see cref="WithStateStore(Func{IServiceProvider, Func{NpgsqlConnection}})"/>.
	/// </summary>
	internal Func<IServiceProvider, Func<NpgsqlConnection>>? StateConnectionFactory { get; private set; }

	/// <summary>
	/// Gets the state store configure callback, if provided.
	/// </summary>
	internal Action<ICdcStateStoreBuilder>? StateStoreConfigure { get; private set; }

	/// <summary>
	/// Gets the source BindConfiguration section path, if set.
	/// </summary>
	internal string? SourceBindConfigurationPath { get; private set; }

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
		_options.Replication.UseBinaryProtocol = useBinary;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder AutoCreateSlot(bool autoCreate = true)
	{
		_options.Replication.AutoCreateSlot = autoCreate;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder WithStateStore(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		StateConnectionString = connectionString;
		StateConnectionFactory = _ => () => new NpgsqlConnection(connectionString);
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ArgumentNullException.ThrowIfNull(configure);

		StateConnectionString = connectionString;
		StateConnectionFactory = _ => () => new NpgsqlConnection(connectionString);
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder WithStateStore(Func<IServiceProvider, Func<NpgsqlConnection>> stateConnectionFactory)
	{
		ArgumentNullException.ThrowIfNull(stateConnectionFactory);

		StateConnectionFactory = stateConnectionFactory;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder WithStateStore(
		Func<IServiceProvider, Func<NpgsqlConnection>> stateConnectionFactory,
		Action<ICdcStateStoreBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(stateConnectionFactory);
		ArgumentNullException.ThrowIfNull(configure);

		StateConnectionFactory = stateConnectionFactory;
		StateStoreConfigure = configure;
		return this;
	}

	/// <inheritdoc/>
	public IPostgresCdcBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		SourceBindConfigurationPath = sectionPath;
		return this;
	}

	/// <summary>Gets the connection string name for resolution from IConfiguration.</summary>
	internal string? SourceConnectionStringName { get; private set; }

	/// <inheritdoc/>
	public IPostgresCdcBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		SourceConnectionStringName = name;
		return this;
	}
}
