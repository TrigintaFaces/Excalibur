// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.Firestore;

/// <summary>
/// Internal implementation of <see cref="ICdcStateStoreBuilder"/> for Firestore CDC.
/// Firestore uses project-based auth, not connection strings, and has no schema concept.
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
