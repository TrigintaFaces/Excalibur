// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for CosmosDB CDC.
/// Maps SchemaName → DatabaseId, TableName → ContainerId.
/// </summary>
internal sealed class CosmosDbCdcStateStoreBuilder : ICdcStateStoreBuilder
{
	private readonly CosmosDbCdcStateStoreOptions _options;

	internal CosmosDbCdcStateStoreBuilder(CosmosDbCdcStateStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the BindConfiguration section path, if set.</summary>
	internal string? BindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	/// <remarks>Maps to <see cref="CosmosDbCdcStateStoreOptions.DatabaseId"/>.</remarks>
	public ICdcStateStoreBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		_options.DatabaseId = schema;
		return this;
	}

	/// <inheritdoc/>
	/// <remarks>Maps to <see cref="CosmosDbCdcStateStoreOptions.ContainerId"/>.</remarks>
	public ICdcStateStoreBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		_options.ContainerId = tableName;
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
