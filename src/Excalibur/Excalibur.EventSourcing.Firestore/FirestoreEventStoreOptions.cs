// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Firestore;

/// <summary>
/// Configuration options for the Firestore event store.
/// </summary>
public sealed class FirestoreEventStoreOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the events collection name.
	/// </summary>
	/// <value>Defaults to "events".</value>
	[Required]
	public string EventsCollectionName { get; set; } = "events";

	/// <summary>
	/// Gets or sets the path to the credentials JSON file.
	/// </summary>
	public string? CredentialsPath { get; set; }

	/// <summary>
	/// Gets or sets the credentials JSON content directly.
	/// </summary>
	public string? CredentialsJson { get; set; }

	/// <summary>
	/// Gets or sets the Firestore emulator host (for local development).
	/// </summary>
	public string? EmulatorHost { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use batched writes for appending events.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool UseBatchedWrites { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum batch size for writes.
	/// </summary>
	/// <value>Defaults to 500 (Firestore limit).</value>
	[Range(1, 500)]
	public int MaxBatchSize { get; set; } = 500;

	/// <summary>
	/// Gets or sets a value indicating whether to create the collection if it doesn't exist.
	/// </summary>
	/// <value>Defaults to <see langword="true"/>.</value>
	public bool CreateCollectionIfNotExists { get; set; } = true;

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ProjectId) && string.IsNullOrWhiteSpace(EmulatorHost))
		{
			throw new InvalidOperationException("ProjectId is required unless using the emulator.");
		}
	}
}
