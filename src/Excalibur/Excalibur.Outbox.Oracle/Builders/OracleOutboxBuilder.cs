// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Internal implementation of the Oracle outbox builder.
/// </summary>
internal sealed class OracleOutboxBuilder : IOracleOutboxBuilder
{
	private readonly OracleOutboxStoreOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleOutboxBuilder"/> class.
	/// </summary>
	/// <param name="options">The Oracle outbox options to configure.</param>
	public OracleOutboxBuilder(OracleOutboxStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Gets the configured connection string, if set via <see cref="ConnectionString"/>.
	/// </summary>
	internal string? ConfiguredConnectionString { get; private set; }

	/// <summary>
	/// Gets the configured IDb factory, if set via <see cref="ConnectionFactory"/>.
	/// </summary>
	internal Func<IServiceProvider, IDb>? ConfiguredDbFactory { get; private set; }

	internal string? ConnectionStringNameValue { get; private set; }
	internal string? BindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	public IOracleOutboxBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		ConfiguredConnectionString = connectionString;
		ConfiguredDbFactory = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleOutboxBuilder ConnectionFactory(Func<IServiceProvider, IDb> dbFactory)
	{
		ArgumentNullException.ThrowIfNull(dbFactory);
		ConfiguredDbFactory = dbFactory;
		ConfiguredConnectionString = null;
		ConnectionStringNameValue = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleOutboxBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public IOracleOutboxBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.OutboxTableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public IOracleOutboxBuilder DeadLetterTableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.DeadLetterTableName = tableName;
		return this;
	}

	/// <summary>Sets the command timeout for Oracle operations.</summary>
	public IOracleOutboxBuilder CommandTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Command timeout must be positive.");
		}

		// OracleOutboxStoreOptions inherits from OutboxOptions which has BatchProcessing sub-options
		_options.BatchProcessing.BatchProcessingTimeout = timeout;
		return this;
	}

	/// <summary>Sets the reservation timeout for message processing.</summary>
	public IOracleOutboxBuilder ReservationTimeout(TimeSpan timeout)
	{
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Reservation timeout must be positive.");
		}

		_options.ReservationTimeout = (int)timeout.TotalSeconds;
		return this;
	}

	/// <summary>Sets the maximum delivery attempts before dead lettering.</summary>
	public IOracleOutboxBuilder MaxAttempts(int maxAttempts)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAttempts, 0);
		_options.MaxAttempts = maxAttempts;
		return this;
	}

	/// <inheritdoc/>
	public IOracleOutboxBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ConnectionStringNameValue = name;
		ConfiguredConnectionString = null;
		ConfiguredDbFactory = null;
		BindConfigurationPath = null;
		return this;
	}

	/// <inheritdoc/>
	public IOracleOutboxBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
		BindConfigurationPath = sectionPath;
		ConfiguredConnectionString = null;
		ConfiguredDbFactory = null;
		ConnectionStringNameValue = null;
		return this;
	}
}
