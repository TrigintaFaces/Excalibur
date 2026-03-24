// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for Firestore CDC.
/// Maps SchemaName -> no-op (Firestore has no schema concept), TableName -> CollectionName.
/// </summary>
internal sealed class FirestoreCdcStateStoreBuilder : ICdcStateStoreBuilder
{
	private readonly FirestoreCdcStateStoreOptions _options;

	internal FirestoreCdcStateStoreBuilder(FirestoreCdcStateStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <summary>Gets the BindConfiguration section path, if set.</summary>
	internal string? BindConfigurationPath { get; private set; }

	/// <summary>Gets the project ID / connection string set via <see cref="ConnectionString"/>.</summary>
	internal string? StateProjectId { get; private set; }

	/// <summary>Gets the connection string name to resolve from configuration, if set.</summary>
	internal string? StateConnectionStringName { get; private set; }

	/// <inheritdoc/>
	/// <remarks>For Firestore, this sets the project ID for the state store.</remarks>
	public ICdcStateStoreBuilder ConnectionString(string connectionString)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
		StateProjectId = connectionString;
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
	/// <remarks>No-op for Firestore as it does not have a schema concept. The value is accepted but ignored.</remarks>
	public ICdcStateStoreBuilder SchemaName(string schema)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(schema);
		// Firestore has no schema concept; accept but ignore.
		return this;
	}

	/// <inheritdoc/>
	/// <remarks>Maps to <see cref="FirestoreCdcStateStoreOptions.CollectionName"/>.</remarks>
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
