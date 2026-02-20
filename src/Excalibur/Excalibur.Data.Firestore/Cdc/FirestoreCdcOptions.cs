// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Configuration options for Firestore CDC processor.
/// </summary>
public sealed class FirestoreCdcOptions
{
	/// <summary>
	/// Gets or sets the Firestore collection path to watch.
	/// </summary>
	/// <remarks>
	/// This can be a simple collection path (e.g., "users") or a
	/// nested path (e.g., "organizations/org1/members").
	/// </remarks>
	[Required]
	public string CollectionPath { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the unique processor name for position tracking.
	/// </summary>
	[Required]
	public string ProcessorName { get; set; } = "cdc-processor";

	/// <summary>
	/// Gets or sets the maximum number of events to process per batch.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int MaxBatchSize { get; set; } = 100;

	/// <summary>
	/// Gets or sets the starting position for stream processing.
	/// </summary>
	/// <remarks>
	/// If null, uses the last confirmed position from the state store,
	/// or starts from the beginning if no position is found.
	/// </remarks>
	public FirestoreCdcPosition? StartPosition { get; set; }

	/// <summary>
	/// Gets or sets the interval between checking for new changes when idle.
	/// </summary>
	/// <remarks>
	/// For push-based Firestore listeners, this represents the minimum interval
	/// between batch processing operations.
	/// </remarks>
	public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum time to wait for new changes in a single batch operation.
	/// </summary>
	public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets a value indicating whether to use a collection group query.
	/// </summary>
	/// <remarks>
	/// When true, listens to all collections with the same ID across the database.
	/// For example, if <see cref="CollectionPath"/> is "comments", this will
	/// listen to all "comments" subcollections under any parent.
	/// </remarks>
	public bool UseCollectionGroup { get; set; }

	/// <summary>
	/// Gets or sets the channel capacity for buffering incoming events.
	/// </summary>
	/// <remarks>
	/// Events are buffered in a bounded channel. If the channel is full,
	/// incoming events will wait until space is available.
	/// </remarks>
	[Range(1, int.MaxValue)]
	public int ChannelCapacity { get; set; } = 1000;

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(CollectionPath))
		{
			throw new InvalidOperationException($"{nameof(CollectionPath)} is required.");
		}

		if (string.IsNullOrWhiteSpace(ProcessorName))
		{
			throw new InvalidOperationException($"{nameof(ProcessorName)} is required.");
		}

		if (MaxBatchSize <= 0)
		{
			throw new InvalidOperationException($"{nameof(MaxBatchSize)} must be greater than zero.");
		}

		if (PollInterval <= TimeSpan.Zero)
		{
			throw new InvalidOperationException($"{nameof(PollInterval)} must be greater than zero.");
		}

		if (MaxWaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException($"{nameof(MaxWaitTime)} must be greater than zero.");
		}

		if (ChannelCapacity <= 0)
		{
			throw new InvalidOperationException($"{nameof(ChannelCapacity)} must be greater than zero.");
		}
	}
}
