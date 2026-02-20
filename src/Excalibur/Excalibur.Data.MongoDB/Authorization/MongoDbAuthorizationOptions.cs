// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.MongoDB.Authorization;

/// <summary>
/// Configuration options for MongoDB authorization stores.
/// </summary>
public sealed class MongoDbAuthorizationOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	/// <value>The connection string. Required.</value>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database name.
	/// </summary>
	/// <value>The database name. Defaults to "authorization".</value>
	[Required]
	public string DatabaseName { get; set; } = "authorization";

	/// <summary>
	/// Gets or sets the collection name for grants.
	/// </summary>
	/// <value>The grants collection name. Defaults to "grants".</value>
	[Required]
	public string GrantsCollectionName { get; set; } = "grants";

	/// <summary>
	/// Gets or sets the collection name for activity groups.
	/// </summary>
	/// <value>The activity groups collection name. Defaults to "activity_groups".</value>
	[Required]
	public string ActivityGroupsCollectionName { get; set; } = "activity_groups";

	/// <summary>
	/// Validates the configuration options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException($"{nameof(ConnectionString)} is required.");
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException($"{nameof(DatabaseName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(GrantsCollectionName))
		{
			throw new InvalidOperationException($"{nameof(GrantsCollectionName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(ActivityGroupsCollectionName))
		{
			throw new InvalidOperationException($"{nameof(ActivityGroupsCollectionName)} is required.");
		}
	}
}
