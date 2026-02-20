// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Represents a record that can be migrated between serialization formats.
/// </summary>
/// <remarks>
/// This interface provides a store-agnostic view of records for migration purposes.
/// Each store implementation (Outbox, Inbox, EventStore) should provide migration
/// record adapters that implement this interface.
/// </remarks>
public interface IMigrationRecord
{
	/// <summary>
	/// Gets the unique identifier of the record.
	/// </summary>
	/// <remarks>
	/// For Outbox/Inbox: typically a GUID or long.
	/// For EventStore: could be a composite of StreamId + Version.
	/// </remarks>
	string Id { get; }

	/// <summary>
	/// Gets the serialized payload bytes including the magic byte header.
	/// </summary>
	byte[] Payload { get; }

	/// <summary>
	/// Gets the CLR type name of the serialized message, if available.
	/// </summary>
	/// <remarks>
	/// Used for deserializing the message to the correct type.
	/// May be null if type information is not stored with the record.
	/// </remarks>
	string? TypeName { get; }
}

/// <summary>
/// Interface for stores that support serializer migration operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines the contract for stores that can participate in
/// serializer migration. Implementations should be provided for:
/// </para>
/// <list type="bullet">
///   <item>SqlServerOutboxStore</item>
///   <item>MessageInbox (Inbox)</item>
///   <item>SqlServerEventStore</item>
/// </list>
/// <para>
/// The migration service uses this interface to:
/// </para>
/// <list type="bullet">
///   <item>Query records by source serializer ID</item>
///   <item>Update payloads with new serialization format</item>
///   <item>Count pending migrations for progress estimation</item>
/// </list>
/// <para>
/// See the migration strategy documentation.
/// </para>
/// </remarks>
public interface IMigrationStore
{
	/// <summary>
	/// Gets the name of this store for logging and diagnostics.
	/// </summary>
	/// <remarks>
	/// Examples: "OutboxStore", "InboxStore", "EventStore".
	/// </remarks>
	string StoreName { get; }

	/// <summary>
	/// Gets a batch of records that need migration from a source serializer.
	/// </summary>
	/// <param name="sourceSerializerId">
	/// The serializer ID to migrate from (magic byte value).
	/// Records with this magic byte in their payload will be returned.
	/// </param>
	/// <param name="targetSerializerId">
	/// The serializer ID to migrate to. Records already in this format are excluded.
	/// </param>
	/// <param name="batchSize">Maximum number of records to return.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A batch of records ready for migration, or empty if no more records need migration.
	/// </returns>
	/// <remarks>
	/// <para>
	/// The query should efficiently filter by magic byte. For SQL Server, this means:
	/// </para>
	/// <code>
	/// WHERE CAST(SUBSTRING(Payload, 1, 1) AS TINYINT) = @SourceSerializerId
	/// </code>
	/// <para>
	/// Records should be returned in a consistent order (e.g., by ID) to ensure
	/// deterministic batch processing.
	/// </para>
	/// </remarks>
	Task<IReadOnlyList<IMigrationRecord>> GetBatchForMigrationAsync(
		byte sourceSerializerId,
		byte targetSerializerId,
		int batchSize,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates the payload of a record with the new serialized format.
	/// </summary>
	/// <param name="recordId">The unique identifier of the record to update.</param>
	/// <param name="newPayload">
	/// The new payload bytes including the magic byte header for the target serializer.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>True if the record was updated, false if not found.</returns>
	/// <remarks>
	/// <para>
	/// The update should be atomic and should verify the record still exists.
	/// The implementation may use optimistic concurrency if appropriate.
	/// </para>
	/// </remarks>
	Task<bool> UpdatePayloadAsync(
		string recordId,
		byte[] newPayload,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the payload for a specific record (for read-back verification).
	/// </summary>
	/// <param name="recordId">The unique identifier of the record.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The payload bytes, or null if the record doesn't exist.</returns>
	Task<byte[]?> GetPayloadAsync(
		string recordId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Counts the total number of records that need migration from a source serializer.
	/// </summary>
	/// <param name="sourceSerializerId">The serializer ID to migrate from.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The count of records needing migration.</returns>
	/// <remarks>
	/// <para>
	/// This count is used for progress estimation. It may be expensive for large tables,
	/// so implementations may choose to return an approximate count or cache the result.
	/// </para>
	/// </remarks>
	Task<int> CountPendingMigrationsAsync(
		byte sourceSerializerId,
		CancellationToken cancellationToken);
}
