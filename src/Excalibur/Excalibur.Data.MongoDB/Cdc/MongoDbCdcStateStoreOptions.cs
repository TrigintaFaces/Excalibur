// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Configuration options for MongoDB CDC state storage.
/// </summary>
public sealed class MongoDbCdcStateStoreOptions
{
	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>The database name. Defaults to "excalibur".</value>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the collection name for CDC state.
	/// </summary>
	/// <value>The collection name. Defaults to "cdc_state".</value>
	[Required]
	public string CollectionName { get; set; } = "cdc_state";

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException("DatabaseName is required.");
		}

		if (string.IsNullOrWhiteSpace(CollectionName))
		{
			throw new InvalidOperationException("CollectionName is required.");
		}
	}
}
