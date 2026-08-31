// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Outbox.Firestore;

/// <summary>
/// Configuration options for the Firestore outbox store.
/// </summary>
public sealed class FirestoreOutboxOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the path to the service account JSON credentials file.
	/// </summary>
	public string? CredentialsPath { get; set; }

	/// <summary>
	/// Gets or sets the JSON content of the service account credentials.
	/// </summary>
	public string? CredentialsJson { get; set; }

	/// <summary>
	/// Gets or sets the Firestore emulator host for local development.
	/// </summary>
	/// <value>Example: "localhost:8080".</value>
	public string? EmulatorHost { get; set; }

	/// <summary>
	/// Gets or sets the outbox collection name.
	/// </summary>
	/// <value>Defaults to "outbox".</value>
	[Required]
	public string CollectionName { get; set; } = "outbox";

	/// <summary>
	/// Gets or sets the default time-to-live for published messages in seconds.
	/// </summary>
	/// <value>Defaults to 7 days (604800 seconds). Set to 0 to disable TTL.</value>
	[Range(0, int.MaxValue)]
	public int DefaultTimeToLiveSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets how long a claim lease is held before it expires, in seconds.
	/// </summary>
	/// <value>Defaults to 120 seconds, matching the relational outbox providers.</value>
	/// <remarks>
	/// Only consulted by the atomic claim (<c>ICloudNativeOutboxStoreClaim</c>). A claimant that dies
	/// mid-delivery releases its messages by letting this window elapse, so nothing has to detect the death
	/// — but a claimant that is merely <i>slow</i> can also have a message taken from under it. Set this
	/// above the maximum time a publish can take, and remember it bounds the duplicate window only up to
	/// the clock skew between claimants.
	/// </remarks>
	[Range(1, int.MaxValue)]
	public int LeaseTimeoutSeconds { get; set; } = 120;

	/// <summary>
	/// Gets or sets the maximum batch size for operations.
	/// </summary>
	/// <value>Defaults to 500 (Firestore batch limit).</value>
	[Range(1, 500)]
	public int MaxBatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets a value indicating whether to create the collection if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	/// <remarks>
	/// Firestore creates collections automatically when documents are added,
	/// but this option can be used to create an initial document to ensure the collection exists.
	/// </remarks>
	public bool CreateCollectionIfNotExists { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum retry attempts for operations.
	/// </summary>
	/// <value>Defaults to 3.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProjectId) && string.IsNullOrWhiteSpace(EmulatorHost))
		{
			throw new InvalidOperationException(
				"Either ProjectId or EmulatorHost must be specified for Firestore configuration.");
		}
	}
}
