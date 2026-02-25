// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MongoDB.Bson;

namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Represents a data change event captured from MongoDB Change Streams.
/// </summary>
public sealed class MongoDbDataChangeEvent
{
	/// <summary>
	/// Gets the resume token position of this change.
	/// </summary>
	public MongoDbCdcPosition Position { get; init; }

	/// <summary>
	/// Gets the database name where the change occurred.
	/// </summary>
	public string DatabaseName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the collection name where the change occurred.
	/// </summary>
	public string CollectionName { get; init; } = string.Empty;

	/// <summary>
	/// Gets the fully qualified namespace (database.collection).
	/// </summary>
	public string FullNamespace => $"{DatabaseName}.{CollectionName}";

	/// <summary>
	/// Gets the type of change (Insert, Update, Replace, Delete, etc.).
	/// </summary>
	public MongoDbDataChangeType ChangeType { get; init; }

	/// <summary>
	/// Gets the document key that identifies the affected document.
	/// </summary>
	/// <remarks>
	/// This typically contains the _id field of the document.
	/// </remarks>
	public BsonDocument? DocumentKey { get; init; }

	/// <summary>
	/// Gets the full document after the change (for insert, update with full document, or replace).
	/// </summary>
	/// <remarks>
	/// Available when FullDocument option is enabled in the change stream.
	/// </remarks>
	public BsonDocument? FullDocument { get; init; }

	/// <summary>
	/// Gets the document as it was before the change (MongoDB 6.0+).
	/// </summary>
	/// <remarks>
	/// Available when FullDocumentBeforeChange option is enabled and the collection
	/// has change stream pre and post images enabled.
	/// </remarks>
	public BsonDocument? FullDocumentBeforeChange { get; init; }

	/// <summary>
	/// Gets the update description containing modified fields and removed fields.
	/// </summary>
	/// <remarks>
	/// Only present for update operations.
	/// </remarks>
	public MongoDbUpdateDescription? UpdateDescription { get; init; }

	/// <summary>
	/// Gets the cluster time when the change occurred.
	/// </summary>
	public BsonTimestamp? ClusterTime { get; init; }

	/// <summary>
	/// Gets the wall clock time when the change was recorded.
	/// </summary>
	public DateTimeOffset? WallTime { get; init; }

	/// <summary>
	/// Creates a new instance for an insert operation.
	/// </summary>
	public static MongoDbDataChangeEvent CreateInsert(
		MongoDbCdcPosition position,
		string databaseName,
		string collectionName,
		BsonDocument? documentKey,
		BsonDocument? fullDocument,
		BsonTimestamp? clusterTime,
		DateTimeOffset? wallTime)
	{
		return new MongoDbDataChangeEvent
		{
			Position = position,
			DatabaseName = databaseName,
			CollectionName = collectionName,
			ChangeType = MongoDbDataChangeType.Insert,
			DocumentKey = documentKey,
			FullDocument = fullDocument,
			ClusterTime = clusterTime,
			WallTime = wallTime,
		};
	}

	/// <summary>
	/// Creates a new instance for an update operation.
	/// </summary>
	public static MongoDbDataChangeEvent CreateUpdate(
		MongoDbCdcPosition position,
		string databaseName,
		string collectionName,
		BsonDocument? documentKey,
		BsonDocument? fullDocument,
		BsonDocument? fullDocumentBeforeChange,
		MongoDbUpdateDescription? updateDescription,
		BsonTimestamp? clusterTime,
		DateTimeOffset? wallTime)
	{
		return new MongoDbDataChangeEvent
		{
			Position = position,
			DatabaseName = databaseName,
			CollectionName = collectionName,
			ChangeType = MongoDbDataChangeType.Update,
			DocumentKey = documentKey,
			FullDocument = fullDocument,
			FullDocumentBeforeChange = fullDocumentBeforeChange,
			UpdateDescription = updateDescription,
			ClusterTime = clusterTime,
			WallTime = wallTime,
		};
	}

	/// <summary>
	/// Creates a new instance for a replace operation.
	/// </summary>
	public static MongoDbDataChangeEvent CreateReplace(
		MongoDbCdcPosition position,
		string databaseName,
		string collectionName,
		BsonDocument? documentKey,
		BsonDocument? fullDocument,
		BsonDocument? fullDocumentBeforeChange,
		BsonTimestamp? clusterTime,
		DateTimeOffset? wallTime)
	{
		return new MongoDbDataChangeEvent
		{
			Position = position,
			DatabaseName = databaseName,
			CollectionName = collectionName,
			ChangeType = MongoDbDataChangeType.Replace,
			DocumentKey = documentKey,
			FullDocument = fullDocument,
			FullDocumentBeforeChange = fullDocumentBeforeChange,
			ClusterTime = clusterTime,
			WallTime = wallTime,
		};
	}

	/// <summary>
	/// Creates a new instance for a delete operation.
	/// </summary>
	public static MongoDbDataChangeEvent CreateDelete(
		MongoDbCdcPosition position,
		string databaseName,
		string collectionName,
		BsonDocument? documentKey,
		BsonDocument? fullDocumentBeforeChange,
		BsonTimestamp? clusterTime,
		DateTimeOffset? wallTime)
	{
		return new MongoDbDataChangeEvent
		{
			Position = position,
			DatabaseName = databaseName,
			CollectionName = collectionName,
			ChangeType = MongoDbDataChangeType.Delete,
			DocumentKey = documentKey,
			FullDocumentBeforeChange = fullDocumentBeforeChange,
			ClusterTime = clusterTime,
			WallTime = wallTime,
		};
	}

	/// <summary>
	/// Creates a new instance for a drop operation.
	/// </summary>
	public static MongoDbDataChangeEvent CreateDrop(
		MongoDbCdcPosition position,
		string databaseName,
		string collectionName,
		BsonTimestamp? clusterTime,
		DateTimeOffset? wallTime)
	{
		return new MongoDbDataChangeEvent
		{
			Position = position,
			DatabaseName = databaseName,
			CollectionName = collectionName,
			ChangeType = MongoDbDataChangeType.Drop,
			ClusterTime = clusterTime,
			WallTime = wallTime,
		};
	}

	/// <summary>
	/// Creates a new instance for an invalidate operation.
	/// </summary>
	public static MongoDbDataChangeEvent CreateInvalidate(
		MongoDbCdcPosition position,
		BsonTimestamp? clusterTime,
		DateTimeOffset? wallTime)
	{
		return new MongoDbDataChangeEvent
		{
			Position = position,
			ChangeType = MongoDbDataChangeType.Invalidate,
			ClusterTime = clusterTime,
			WallTime = wallTime,
		};
	}
}

/// <summary>
/// Describes the changes made in an update operation.
/// </summary>
public sealed class MongoDbUpdateDescription
{
	/// <summary>
	/// Gets the fields that were updated and their new values.
	/// </summary>
	public BsonDocument? UpdatedFields { get; init; }

	/// <summary>
	/// Gets the names of fields that were removed.
	/// </summary>
	public IReadOnlyList<string> RemovedFields { get; init; } = [];

	/// <summary>
	/// Gets the truncated arrays with their new size.
	/// </summary>
	/// <remarks>
	/// Available in MongoDB 5.0+.
	/// </remarks>
	public IReadOnlyList<MongoDbArrayTruncation> TruncatedArrays { get; init; } = [];
}

/// <summary>
/// Describes an array truncation in an update operation.
/// </summary>
public sealed class MongoDbArrayTruncation
{
	/// <summary>
	/// Gets the field path of the truncated array.
	/// </summary>
	public string Field { get; init; } = string.Empty;

	/// <summary>
	/// Gets the new size of the truncated array.
	/// </summary>
	public int NewSize { get; init; }
}
