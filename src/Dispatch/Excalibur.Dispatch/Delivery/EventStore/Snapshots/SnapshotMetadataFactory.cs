// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Provides factory methods for creating snapshot metadata instances in the event sourcing system. This factory encapsulates the logic for
/// extracting relevant information from events and constructing proper metadata structures for snapshot storage and retrieval.
/// </summary>
/// <remarks>
/// Snapshot metadata is critical for maintaining consistency and enabling proper versioning in event sourcing implementations. The factory
/// ensures that all required metadata fields are properly populated from source events and configuration parameters.
/// </remarks>
public static class SnapshotMetadataFactory
{
	/// <summary>
	/// Creates a new snapshot metadata instance from the specified event and versioning information. This method extracts timing and
	/// identification data from the last applied event to ensure proper snapshot versioning and consistency tracking.
	/// </summary>
	/// <typeparam name="TKey"> The type of key used to identify the aggregate or entity, must not be null. </typeparam>
	/// <param name="lastEvent"> The most recent event applied to the aggregate before creating the snapshot. </param>
	/// <param name="serializerVersion"> The version identifier of the serializer used to create the snapshot data. </param>
	/// <param name="snapshotVersion"> The version identifier of the snapshot format or schema. </param>
	/// <returns> A new <see cref="SnapshotMetadata" /> instance containing timing, identification, and versioning information. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="lastEvent" /> is null. </exception>
	/// <remarks>
	/// The metadata captures essential information needed for snapshot validation and evolution:
	/// - Event timing ensures chronological consistency
	/// - Event ID provides traceability back to the source event
	/// - Version information enables proper deserialization and migration handling This information is crucial for maintaining data
	/// integrity across snapshot operations.
	/// </remarks>
	public static SnapshotMetadata Create<TKey>(
		IEventStoreMessage<TKey> lastEvent,
		string serializerVersion,
		string snapshotVersion)
		where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(lastEvent);

		return new SnapshotMetadata
		{
			LastAppliedEventTimestamp = lastEvent.OccurredOn,
			LastAppliedEventId = lastEvent.EventId,
			SerializerVersion = serializerVersion,
			SnapshotVersion = snapshotVersion,
		};
	}
}
