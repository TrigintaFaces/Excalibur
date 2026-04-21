// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.DynamoDb;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for DynamoDB CDC.
/// DynamoDB uses IAM/SDK credentials, not connection strings, and has no schema concept.
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
