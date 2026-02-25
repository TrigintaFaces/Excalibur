// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Represents a data change event captured from CosmosDb Change Feed.
/// </summary>
public sealed class CosmosDbDataChangeEvent
{
	/// <summary>
	/// Gets the position of this change in the Change Feed.
	/// </summary>
	public CosmosDbCdcPosition Position { get; init; } = CosmosDbCdcPosition.Beginning();

	/// <summary>
	/// Gets the type of change (Insert, Update, Delete).
	/// </summary>
	public CosmosDbDataChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets the document ID.
	/// </summary>
	public string DocumentId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the partition key value, if available.
	/// </summary>
	public string? PartitionKey { get; init; }

	/// <summary>
	/// Gets the full document (current state).
	/// </summary>
	/// <remarks>
	/// Available for Insert and Update events. May be null for Delete events
	/// in AllVersionsAndDeletes mode.
	/// </remarks>
	public JsonDocument? Document { get; init; }

	/// <summary>
	/// Gets the document as it was before the change.
	/// </summary>
	/// <remarks>
	/// Only available in AllVersionsAndDeletes mode for Update and Delete events.
	/// </remarks>
	public JsonDocument? PreviousDocument { get; init; }

	/// <summary>
	/// Gets the timestamp when the change occurred.
	/// </summary>
	public DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets the logical sequence number (LSN) from the Change Feed.
	/// </summary>
	/// <remarks>
	/// The _lsn property is a monotonically increasing value within a partition.
	/// </remarks>
	public long Lsn { get; init; }

	/// <summary>
	/// Gets the ETag of the document at this version.
	/// </summary>
	public string? ETag { get; init; }

	/// <summary>
	/// Creates a new instance for an insert event.
	/// </summary>
	public static CosmosDbDataChangeEvent CreateInsert(
		CosmosDbCdcPosition position,
		string documentId,
		string? partitionKey,
		JsonDocument document,
		DateTimeOffset timestamp,
		long lsn,
		string? etag)
	{
		return new CosmosDbDataChangeEvent
		{
			Position = position,
			ChangeType = CosmosDbDataChangeType.Insert,
			DocumentId = documentId,
			PartitionKey = partitionKey,
			Document = document,
			Timestamp = timestamp,
			Lsn = lsn,
			ETag = etag,
		};
	}

	/// <summary>
	/// Creates a new instance for an update event.
	/// </summary>
	public static CosmosDbDataChangeEvent CreateUpdate(
		CosmosDbCdcPosition position,
		string documentId,
		string? partitionKey,
		JsonDocument document,
		JsonDocument? previousDocument,
		DateTimeOffset timestamp,
		long lsn,
		string? etag)
	{
		return new CosmosDbDataChangeEvent
		{
			Position = position,
			ChangeType = CosmosDbDataChangeType.Update,
			DocumentId = documentId,
			PartitionKey = partitionKey,
			Document = document,
			PreviousDocument = previousDocument,
			Timestamp = timestamp,
			Lsn = lsn,
			ETag = etag,
		};
	}

	/// <summary>
	/// Creates a new instance for a delete event.
	/// </summary>
	/// <remarks>
	/// Delete events are only available in AllVersionsAndDeletes mode.
	/// </remarks>
	public static CosmosDbDataChangeEvent CreateDelete(
		CosmosDbCdcPosition position,
		string documentId,
		string? partitionKey,
		JsonDocument? previousDocument,
		DateTimeOffset timestamp,
		long lsn)
	{
		return new CosmosDbDataChangeEvent
		{
			Position = position,
			ChangeType = CosmosDbDataChangeType.Delete,
			DocumentId = documentId,
			PartitionKey = partitionKey,
			PreviousDocument = previousDocument,
			Timestamp = timestamp,
			Lsn = lsn,
		};
	}
}
