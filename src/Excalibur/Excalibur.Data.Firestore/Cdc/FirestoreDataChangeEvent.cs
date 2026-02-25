// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Represents a data change event captured from Firestore Realtime Listeners.
/// </summary>
public sealed class FirestoreDataChangeEvent
{
	/// <summary>
	/// Gets the position of this change in the stream.
	/// </summary>
	public FirestoreCdcPosition Position { get; init; } = null!;

	/// <summary>
	/// Gets the type of change (Added, Modified, Removed).
	/// </summary>
	public FirestoreDataChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets the collection path this change came from.
	/// </summary>
	public string CollectionPath { get; init; } = string.Empty;

	/// <summary>
	/// Gets the document ID within the collection.
	/// </summary>
	public string DocumentId { get; init; } = string.Empty;

	/// <summary>
	/// Gets the document data after the change.
	/// </summary>
	/// <remarks>
	/// Available for Added and Modified events. Null for Removed events.
	/// </remarks>
	public IReadOnlyDictionary<string, object>? DocumentData { get; init; }

	/// <summary>
	/// Gets the timestamp when the change occurred.
	/// </summary>
	public DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets the document update time from Firestore metadata.
	/// </summary>
	/// <remarks>
	/// This is the server-side timestamp from Firestore, used for position tracking.
	/// </remarks>
	public DateTimeOffset? UpdateTime { get; init; }

	/// <summary>
	/// Gets the document create time from Firestore metadata.
	/// </summary>
	public DateTimeOffset? CreateTime { get; init; }

	/// <summary>
	/// Creates a new instance for an added event.
	/// </summary>
	/// <param name="position">The CDC position.</param>
	/// <param name="collectionPath">The collection path.</param>
	/// <param name="documentId">The document ID.</param>
	/// <param name="documentData">The document data.</param>
	/// <param name="timestamp">The event timestamp.</param>
	/// <param name="updateTime">The document update time.</param>
	/// <param name="createTime">The document create time.</param>
	/// <returns>A new change event.</returns>
	public static FirestoreDataChangeEvent CreateAdded(
		FirestoreCdcPosition position,
		string collectionPath,
		string documentId,
		IReadOnlyDictionary<string, object>? documentData,
		DateTimeOffset timestamp,
		DateTimeOffset? updateTime,
		DateTimeOffset? createTime)
	{
		ArgumentNullException.ThrowIfNull(position);
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		return new FirestoreDataChangeEvent
		{
			Position = position,
			ChangeType = FirestoreDataChangeType.Added,
			CollectionPath = collectionPath,
			DocumentId = documentId,
			DocumentData = documentData,
			Timestamp = timestamp,
			UpdateTime = updateTime,
			CreateTime = createTime,
		};
	}

	/// <summary>
	/// Creates a new instance for a modified event.
	/// </summary>
	/// <param name="position">The CDC position.</param>
	/// <param name="collectionPath">The collection path.</param>
	/// <param name="documentId">The document ID.</param>
	/// <param name="documentData">The document data.</param>
	/// <param name="timestamp">The event timestamp.</param>
	/// <param name="updateTime">The document update time.</param>
	/// <param name="createTime">The document create time.</param>
	/// <returns>A new change event.</returns>
	public static FirestoreDataChangeEvent CreateModified(
		FirestoreCdcPosition position,
		string collectionPath,
		string documentId,
		IReadOnlyDictionary<string, object>? documentData,
		DateTimeOffset timestamp,
		DateTimeOffset? updateTime,
		DateTimeOffset? createTime)
	{
		ArgumentNullException.ThrowIfNull(position);
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		return new FirestoreDataChangeEvent
		{
			Position = position,
			ChangeType = FirestoreDataChangeType.Modified,
			CollectionPath = collectionPath,
			DocumentId = documentId,
			DocumentData = documentData,
			Timestamp = timestamp,
			UpdateTime = updateTime,
			CreateTime = createTime,
		};
	}

	/// <summary>
	/// Creates a new instance for a removed event.
	/// </summary>
	/// <param name="position">The CDC position.</param>
	/// <param name="collectionPath">The collection path.</param>
	/// <param name="documentId">The document ID.</param>
	/// <param name="timestamp">The event timestamp.</param>
	/// <returns>A new change event.</returns>
	public static FirestoreDataChangeEvent CreateRemoved(
		FirestoreCdcPosition position,
		string collectionPath,
		string documentId,
		DateTimeOffset timestamp)
	{
		ArgumentNullException.ThrowIfNull(position);
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		return new FirestoreDataChangeEvent
		{
			Position = position,
			ChangeType = FirestoreDataChangeType.Removed,
			CollectionPath = collectionPath,
			DocumentId = documentId,
			DocumentData = null,
			Timestamp = timestamp,
			UpdateTime = null,
			CreateTime = null,
		};
	}
}
