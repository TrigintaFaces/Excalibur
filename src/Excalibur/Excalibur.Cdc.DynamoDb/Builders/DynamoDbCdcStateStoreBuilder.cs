// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for DynamoDB CDC.
/// Maps SchemaName -> no-op (DynamoDB has no schema concept), TableName -> TableName.
/// </summary>
internal sealed class DynamoDbCdcStateStoreBuilder : ICdcStateStoreBuilder
{
	private readonly DynamoDbCdcStateStoreOptions _options;

	internal DynamoDbCdcStateStoreBuilder(DynamoDbCdcStateStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the BindConfiguration section path, if set.</summary>
	internal string? BindConfigurationPath { get; private set; }

	/// <inheritdoc/>
	/// <remarks>No-op for DynamoDB as it does not have a schema concept. The value is accepted but ignored.</remarks>
	public ICdcStateStoreBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		// DynamoDB has no schema concept; accept but ignore.
		return this;
	}

	/// <inheritdoc/>
	/// <remarks>Maps to <see cref="DynamoDbCdcStateStoreOptions.TableName"/>.</remarks>
	public ICdcStateStoreBuilder TableName(string tableName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
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
