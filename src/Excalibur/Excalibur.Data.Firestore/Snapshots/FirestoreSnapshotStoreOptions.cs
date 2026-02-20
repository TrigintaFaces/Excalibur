// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Snapshots;

/// <summary>
/// Configuration options for the Firestore snapshot store.
/// </summary>
public sealed class FirestoreSnapshotStoreOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the collection name for snapshots.
	/// </summary>
	/// <value>Defaults to "snapshots".</value>
	[Required]
	public string CollectionName { get; set; } = "snapshots";

	/// <summary>
	/// Gets or sets the path to the service account JSON credentials file.
	/// If not specified, uses application default credentials.
	/// </summary>
	public string? CredentialsPath { get; set; }

	/// <summary>
	/// Gets or sets the JSON content of the service account credentials.
	/// Alternative to <see cref="CredentialsPath"/> for environments like containers.
	/// </summary>
	public string? CredentialsJson { get; set; }

	/// <summary>
	/// Gets or sets the Firestore emulator host for local development.
	/// Example: "localhost:8080".
	/// </summary>
	public string? EmulatorHost { get; set; }

	/// <summary>
	/// Gets or sets the default time to live for snapshots in seconds.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. Defaults to 0.
	/// Note: Firestore doesn't have native TTL - use DeleteSnapshotsOlderThanAsync for cleanup.
	/// </remarks>
	/// <value>Defaults to 0 (no TTL).</value>
	[Range(0, int.MaxValue)]
	public int DefaultTtlSeconds { get; set; }

	/// <summary>
	/// Gets or sets the timeout for operations in seconds.
	/// </summary>
	/// <value>Defaults to 30 seconds.</value>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets the maximum batch size for bulk operations.
	/// </summary>
	/// <remarks>
	/// Firestore has a limit of 500 documents per batch operation.
	/// </remarks>
	/// <value>Defaults to 500.</value>
	[Range(1, 500)]
	public int MaxBatchSize { get; set; } = 500;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProjectId) && string.IsNullOrWhiteSpace(EmulatorHost))
		{
			throw new InvalidOperationException(
				"Either ProjectId or EmulatorHost must be specified.");
		}

		if (string.IsNullOrWhiteSpace(CollectionName))
		{
			throw new InvalidOperationException("CollectionName is required.");
		}
	}
}
