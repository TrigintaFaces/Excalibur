// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Outbox.MongoDB;

public sealed partial class MongoDbOutboxStore
{
	private async Task EnsureSimpleIdentityCollectionAsync(CancellationToken token)
	{
		var collection = _collection ?? throw new InvalidOperationException("MongoDB outbox collection is not configured.");
		// An injected collection already has an effective namespace; mutable options cannot retarget this probe.
		var database = collection.Database;
		var name = collection.CollectionNamespace.CollectionName;
		var physicalName = collection.CollectionNamespace.FullName;
		var descriptions = await ReadIdentityCollectionAsync(database, name, token).ConfigureAwait(false);
		if (descriptions.Count == 0)
		{
			try
			{
				await database.CreateCollectionAsync(name, new CreateCollectionOptions { Collation = Collation.Simple }, token).ConfigureAwait(false);
			}
			catch (MongoCommandException error) when (error.Code == 48)
			{
				// Only NamespaceExists is an expected race. Verify the winner rather than trusting its options.
			}
			descriptions = await ReadIdentityCollectionAsync(database, name, token).ConfigureAwait(false);
		}
		if (descriptions.Count != 1 || !descriptions[0].TryGetValue("type", out var type)
			|| !type.IsString || type.AsString != "collection"
			|| !descriptions[0].TryGetValue("options", out var options) || !options.IsBsonDocument)
		{
			throw UnsupportedIdentitySchema(physicalName, "the namespace is not a standard collection");
		}
		var collectionOptions = options.AsBsonDocument;
		if (!HasSimpleCollation(collectionOptions)
			|| collectionOptions.Contains("viewOn") || collectionOptions.Contains("timeseries")
			|| (collectionOptions.TryGetValue("capped", out var capped) && (!capped.IsBoolean || capped.AsBoolean)))
		{
			throw UnsupportedIdentitySchema(physicalName, "the collection is not an ordinary simple-binary identity namespace");
		}
		using var cursor = await collection.Indexes.ListAsync(token).ConfigureAwait(false);
		var indexes = await cursor.ToListAsync(token).ConfigureAwait(false);
		var identifiers = indexes.Where(index => index.TryGetValue("name", out var indexName) && indexName.IsString
			&& indexName.AsString == "_id_").ToArray();
		if (identifiers.Length != 1 || !IsSimpleUniqueIdentifier(identifiers[0]))
		{
			throw UnsupportedIdentitySchema(physicalName, "the required simple unique single-field _id index is absent or incompatible");
		}
	}

	private static async Task<List<BsonDocument>> ReadIdentityCollectionAsync(IMongoDatabase database, string name, CancellationToken token)
	{
		using var cursor = await database.ListCollectionsAsync(new ListCollectionsOptions
		{
			Filter = new BsonDocument("name", name),
		}, token).ConfigureAwait(false);
		return await cursor.ToListAsync(token).ConfigureAwait(false);
	}

	private static bool HasSimpleCollation(BsonDocument metadata) => !metadata.TryGetValue("collation", out var collation)
		|| (collation.IsBsonDocument && collation.AsBsonDocument.TryGetValue("locale", out var locale)
			&& locale.IsString && string.Equals(locale.AsString, "simple", StringComparison.Ordinal));

	private static bool IsSimpleUniqueIdentifier(BsonDocument index)
	{
		if (!index.TryGetValue("key", out var key) || !key.IsBsonDocument || key.AsBsonDocument.ElementCount != 1
			|| !key.AsBsonDocument.TryGetValue("_id", out var direction) || !direction.IsInt32 || direction.AsInt32 != 1
			|| !HasSimpleCollation(index) || index.Contains("partialFilterExpression"))
		{
			return false;
		}
		// MongoDB's canonical _id_ index is intrinsically unique and normally omits the unique field.
		if (index.TryGetValue("unique", out var unique) && (!unique.IsBoolean || !unique.AsBoolean)) { return false; }
		return !index.TryGetValue("sparse", out var sparse) || (sparse.IsBoolean && !sparse.AsBoolean);
	}

	private static InvalidOperationException UnsupportedIdentitySchema(string name, string reason) => new(
		$"MongoDB outbox collection '{name}' requires simple (binary) collation and its simple unique _id index: {reason}. " +
		"Use a dedicated simple outbox collection. Quiesce workers before a separately planned manual migration; the provider does not change existing data or indexes.");
}
