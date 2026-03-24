// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for Postgres CDC.
/// Configures <see cref="PostgresCdcStateStoreOptions"/>.
/// </summary>
internal sealed class PostgresCdcStateStoreBuilder : ICdcStateStoreBuilder
{
	private readonly PostgresCdcStateStoreOptions _options;

	internal PostgresCdcStateStoreBuilder(PostgresCdcStateStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>
	/// Gets the BindConfiguration section path, if set.
	/// </summary>
	internal string? BindConfigurationPath { get; private set; }

	/// <summary>
	/// Gets the state store connection string, if set via <see cref="ConnectionString"/>.
	/// </summary>
	internal string? StateConnectionString { get; private set; }

	/// <summary>
	/// Gets the connection string name for resolution from IConfiguration, if set via <see cref="ConnectionStringName"/>.
	/// </summary>
	internal string? StateConnectionStringName { get; private set; }

	/// <inheritdoc/>
	public ICdcStateStoreBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

		StateConnectionString = connectionString;
		return this;
	}

	/// <inheritdoc/>
	public ICdcStateStoreBuilder ConnectionStringName(string name)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		StateConnectionStringName = name;
		return this;
	}

	/// <inheritdoc/>
	public ICdcStateStoreBuilder SchemaName(string schema)
	{
		if (string.IsNullOrWhiteSpace(schema))
		{
			throw new ArgumentException("Schema name cannot be null or whitespace.", nameof(schema));
		}

		_options.SchemaName = schema;
		return this;
	}

	/// <inheritdoc/>
	public ICdcStateStoreBuilder TableName(string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be null or whitespace.", nameof(tableName));
		}

		_options.TableName = tableName;
		return this;
	}

	/// <inheritdoc/>
	public ICdcStateStoreBuilder BindConfiguration(string sectionPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		BindConfigurationPath = sectionPath;
		return this;
	}
}
