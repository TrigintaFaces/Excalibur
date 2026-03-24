// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for MongoDB CDC.
/// Maps SchemaName -> DatabaseName, TableName -> CollectionName.
/// </summary>
internal sealed class MongoDbCdcStateStoreBuilder : ICdcStateStoreBuilder
{
	private readonly MongoDbCdcStateStoreOptions _options;

	internal MongoDbCdcStateStoreBuilder(MongoDbCdcStateStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the BindConfiguration section path, if set.</summary>
	internal string? BindConfigurationPath { get; private set; }

	/// <summary>Gets the connection string set via <see cref="ConnectionString"/>.</summary>
	internal string? StateConnectionString { get; private set; }

	/// <summary>Gets the connection string name to resolve from configuration, if set.</summary>
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
	/// <remarks>Maps to <see cref="MongoDbCdcStateStoreOptions.DatabaseName"/>.</remarks>
	public ICdcStateStoreBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		_options.DatabaseName = schema;
		return this;
	}

	/// <inheritdoc/>
	/// <remarks>Maps to <see cref="MongoDbCdcStateStoreOptions.CollectionName"/>.</remarks>
	public ICdcStateStoreBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.CollectionName = tableName;
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
