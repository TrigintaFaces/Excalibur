// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Inbox.Firestore;

/// <summary>
/// Configuration options for the Firestore inbox store.
/// </summary>
public sealed class FirestoreInboxOptions
{
	/// <summary>
	/// Gets or sets the Google Cloud project ID.
	/// </summary>
	public string? ProjectId { get; set; }

	/// <summary>
	/// Gets or sets the collection name for inbox entries.
	/// </summary>
	[Required]
	public string CollectionName { get; set; } = "inbox_messages";

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
	/// Gets or sets the default time to live for inbox entries in seconds.
	/// </summary>
	/// <remarks>
	/// Set to 0 for no expiration. Defaults to 7 days (604800 seconds).
	/// Note: Firestore doesn't have native TTL - use CleanupAllTenantsProcessedEntriesAsync for cleanup,
	/// which sweeps every tenant's processed entries.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int DefaultTtlSeconds { get; set; } = 604800;

	/// <summary>
	/// Gets or sets the timeout for operations in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 30;
	/// <summary>
	/// Gets or sets the maximum number of optimistic-concurrency attempts the store makes when a competing
	/// writer invalidates its read between the read and the conditional write.
	/// </summary>
	/// <value>The attempt bound. Defaults to 5. Must be at least 1.</value>
	/// <remarks>
	/// <para>
	/// Each losing attempt writes nothing, so the entry is left exactly as it was found. When every attempt
	/// loses, the store reports <see cref="Excalibur.Dispatch.InboxMarkFailedOutcome.Undecided"/> rather than throwing — it was
	/// asked, it answered, and the call is safe to re-drive.
	/// </para>
	/// <para>
	/// <b>This bound is configuration, not a constant, because the guarantee it governs must be bindable.</b>
	/// While it was hard-coded, the exhaustion branch could be reached only by losing every race in a row,
	/// so no deterministic arm could assert what the store does there — and a bound that makes its own seam's
	/// required arm unwritable is itself the defect. Lowering it narrows the window a caller tolerates before
	/// being told the outcome is undecided; raising it trades latency under contention for fewer undecided
	/// answers.
	/// </para>
	/// </remarks>
	public int MaxConcurrencyRetries { get; set; } = 5;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (MaxConcurrencyRetries < 1)
		{
			throw new InvalidOperationException(
				"MaxConcurrencyRetries must be at least 1. A bound below 1 skips the conditional write entirely, "
				+ "so the store would report Undecided without ever having attempted the transition.");
		}
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
