// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Provides a base implementation for interacting with MongoDB for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The type of the document to manage in MongoDB.</typeparam>
/// <remarks>
/// <para>
/// This class includes operations for adding, updating, retrieving, deleting, and querying
/// documents, as well as initializing collections and indexes in MongoDB.
/// </para>
/// <para>
/// Documents are stored as flat BSON documents. The MongoDB driver's
/// <see cref="IgnoreExtraElementsConvention"/> is registered globally so that consumer
/// document types silently ignore metadata fields during deserialization.
/// </para>
/// <para>
/// Use this base class to build custom query repositories that share MongoDB collections
/// with <c>IProjectionStore&lt;T&gt;</c> or other stores.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderSearchRepository : MongoDbRepositoryBase&lt;OrderProjection&gt;
/// {
///     public OrderSearchRepository(IMongoClient client, IOptionsMonitor&lt;MongoDbProjectionStoreOptions&gt; options)
///         : base(client,
///                options.Get(nameof(OrderProjection)).DatabaseName,
///                MongoDbProjectionCollectionConvention.GetCollectionName&lt;OrderProjection&gt;(
///                    options.Get(nameof(OrderProjection))))
///     {
///     }
///
///     public override Task InitializeCollectionAsync(CancellationToken ct) => Task.CompletedTask;
/// }
/// </code>
/// </example>
public abstract class MongoDbRepositoryBase<TDocument> : IMongoDbRepositoryBase<TDocument>, IMongoDbRepositoryBaseQuery<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbRepositoryBase{TDocument}"/> class.
	/// </summary>
	/// <param name="client">The MongoDB client instance.</param>
	/// <param name="databaseName">The name of the database to operate on.</param>
	/// <param name="collectionName">The name of the collection to operate on.</param>
	protected MongoDbRepositoryBase(IMongoClient client, string databaseName, string collectionName)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

		Client = client;
		CollectionName = collectionName;

		var database = client.GetDatabase(databaseName);
		Collection = database.GetCollection<BsonDocument>(collectionName);

		MongoDbConventionInitializer.EnsureRegistered();
	}

	/// <summary>
	/// Gets the underlying MongoDB client.
	/// </summary>
	protected IMongoClient Client { get; }

	/// <summary>
	/// Gets the name of the collection this repository operates on.
	/// </summary>
	protected string CollectionName { get; }

	/// <summary>
	/// Gets the underlying MongoDB collection typed as <see cref="BsonDocument"/>.
	/// </summary>
	protected IMongoCollection<BsonDocument> Collection { get; }

	/// <inheritdoc />
	public virtual async Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);
		var document = await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

		if (document is null)
		{
			return null;
		}

		return BsonSerializer.Deserialize<TDocument>(document);
	}

	/// <inheritdoc />
	public virtual async Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
		ArgumentNullException.ThrowIfNull(document);

		var bsonDoc = document.ToBsonDocument();
		bsonDoc["_id"] = documentId;

		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);
		var result = await Collection.ReplaceOneAsync(filter, bsonDoc, new ReplaceOptions { IsUpsert = true }, cancellationToken)
			.ConfigureAwait(false);

		return result.IsAcknowledged;
	}

	/// <inheritdoc />
	public virtual async Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

		var filter = Builders<BsonDocument>.Filter.Eq("_id", documentId);
		var result = await Collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

		return result.IsAcknowledged && result.DeletedCount > 0;
	}

	/// <inheritdoc />
	public virtual async Task<IReadOnlyList<TDocument>> FindAsync(
		FilterDefinition<BsonDocument> filter,
		SortDefinition<BsonDocument>? sort,
		int? skip,
		int? limit,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);

		var findFluent = Collection.Find(filter);

		if (sort is not null)
		{
			findFluent = findFluent.Sort(sort);
		}

		if (skip is not null)
		{
			findFluent = findFluent.Skip(skip.Value);
		}

		if (limit is not null)
		{
			findFluent = findFluent.Limit(limit.Value);
		}

		var documents = await findFluent.ToListAsync(cancellationToken).ConfigureAwait(false);

		var results = new List<TDocument>(documents.Count);
		foreach (var doc in documents)
		{
			results.Add(BsonSerializer.Deserialize<TDocument>(doc));
		}

		return results;
	}

	/// <summary>
	/// Initializes the MongoDB collection (creates indexes, etc.).
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public abstract Task InitializeCollectionAsync(CancellationToken cancellationToken);

}
