// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for MongoDB CDC.
/// MongoDB uses IAM/SDK credentials for auth and does not have a relational schema concept.
/// TableName maps to CollectionName.
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
