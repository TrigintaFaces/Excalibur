// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Authorization;

/// <summary>
/// Configuration options for Firestore authorization stores.
/// </summary>
public sealed class FirestoreAuthorizationOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	/// <value>The Google Cloud project ID.</value>
	[Required]
	public string ProjectId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the Firestore emulator host for local development.
	/// </summary>
	/// <value>The emulator host (e.g., "localhost:8080"). If set, emulator mode is enabled.</value>
	public string? EmulatorHost { get; set; }

	/// <summary>
	/// Gets or sets the path to the Google Cloud credentials JSON file.
	/// </summary>
	/// <value>The credentials file path. If not set and not using emulator, uses default credentials.</value>
	public string? CredentialsPath { get; set; }

	/// <summary>
	/// Gets or sets the Google Cloud credentials JSON content directly.
	/// </summary>
	/// <value>The credentials JSON content. Takes precedence over CredentialsPath if both are set.</value>
	public string? CredentialsJson { get; set; }

	/// <summary>
	/// Gets or sets the collection name for grants.
	/// </summary>
	/// <value>The grants collection name. Defaults to "authorization_grants".</value>
	[Required]
	public string GrantsCollectionName { get; set; } = "authorization_grants";

	/// <summary>
	/// Gets or sets the collection name for activity groups.
	/// </summary>
	/// <value>The activity groups collection name. Defaults to "authorization_activity_groups".</value>
	[Required]
	public string ActivityGroupsCollectionName { get; set; } = "authorization_activity_groups";

	/// <summary>
	/// Gets or sets a value indicating whether to use collection groups for cross-tenant queries.
	/// </summary>
	/// <value><see langword="true"/> to use collection groups; otherwise, <see langword="false"/>. Defaults to false.</value>
	/// <remarks>
	/// When enabled, grants are stored as subcollections under tenant documents.
	/// This requires Firestore collection group indexes to be configured.
	/// </remarks>
	public bool UseCollectionGroups { get; set; }

	/// <summary>
	/// Gets or sets the maximum batch size for write operations.
	/// </summary>
	/// <value>The maximum batch size. Defaults to 500 (Firestore limit).</value>
	[Range(1, 500)]
	public int MaxBatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets the request timeout in seconds.
	/// </summary>
	/// <value>The timeout in seconds. Defaults to 30.</value>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProjectId))
		{
			throw new InvalidOperationException($"{nameof(ProjectId)} is required.");
		}

		if (string.IsNullOrWhiteSpace(GrantsCollectionName))
		{
			throw new InvalidOperationException($"{nameof(GrantsCollectionName)} is required.");
		}

		if (string.IsNullOrWhiteSpace(ActivityGroupsCollectionName))
		{
			throw new InvalidOperationException($"{nameof(ActivityGroupsCollectionName)} is required.");
		}

		if (MaxBatchSize is <= 0 or > 500)
		{
			throw new InvalidOperationException($"{nameof(MaxBatchSize)} must be between 1 and 500.");
		}
	}
}
