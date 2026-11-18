// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;
using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace Excalibur.Data.Tests.MongoDB.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "MongoDB")]
[Trait("Pattern", "Regression")]
public sealed class MongoDbOutboxIdentitySchemaShould : UnitTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RecheckTheActualNamespaceExistsWinnerBeforeAnyMutation(bool safeWinner)
	{
		var harness = new Harness();
		var reads = 0;
		A.CallTo(() => harness.Database.ListCollectionsAsync(A<ListCollectionsOptions>._, A<CancellationToken>._))
			.ReturnsLazily(() => Task.FromResult(Cursor(++reads == 1 ? [] : [Description(safeWinner)])));
		var namespaceExists = new MongoCommandException(new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))),
			"namespace exists", new BsonDocument("create", "actual_collection"),
			new BsonDocument { { "ok", 0 }, { "code", 48 }, { "errmsg", "namespace exists" } });
		namespaceExists.Code.ShouldBe(48);
		A.CallTo(() => harness.Database.CreateCollectionAsync("actual_collection", A<CreateCollectionOptions>._, A<CancellationToken>._))
			.ThrowsAsync(namespaceExists);
		await using var store = harness.CreateStore();
		if (safeWinner)
		{
			await store.StageMessageAsync(Row(), CancellationToken.None);
			A.CallTo(() => harness.Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._))
				.MustHaveHappenedOnceExactly();
		}
		else
		{
			(await Should.ThrowAsync<InvalidOperationException>(async () => await store.StageMessageAsync(Row(), CancellationToken.None)))
				.Message.ShouldContain("actual_db.actual_collection");
			harness.AssertNoMutation();
		}
		reads.ShouldBe(2);
		A.CallTo(() => harness.Database.CreateCollectionAsync("actual_collection",
			A<CreateCollectionOptions>.That.Matches(options => options.Collation.Equals(Collation.Simple)), A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RetryMetadataFailureOrInFlightCancellationWithoutCachingAdmission(bool cancellation)
	{
		var harness = new Harness();
		using var source = new CancellationTokenSource();
		var reads = 0;
		var failure = new InvalidOperationException("metadata unavailable");
		A.CallTo(() => harness.Database.ListCollectionsAsync(A<ListCollectionsOptions>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				if (++reads != 1) { return Task.FromResult(Cursor([Description(true)])); }
				if (!cancellation) { return Task.FromException<IAsyncCursor<BsonDocument>>(failure); }
				source.Cancel();
				return Task.FromCanceled<IAsyncCursor<BsonDocument>>(source.Token);
			});
		await using var store = harness.CreateStore();
		if (cancellation)
		{
			var error = await Should.ThrowAsync<OperationCanceledException>(async () => await store.StageMessageAsync(Row(), source.Token));
			error.CancellationToken.ShouldBe(source.Token);
		}
		else
		{
			(await Should.ThrowAsync<InvalidOperationException>(async () => await store.StageMessageAsync(Row(), source.Token))).ShouldBeSameAs(failure);
		}
		harness.AssertNoMutation();
		await store.StageMessageAsync(Row(), CancellationToken.None);
		reads.ShouldBe(2);
		A.CallTo(() => harness.Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	public async Task RejectIncompatibleIdentifierMetadataBeforeAutomaticIndexesOrRows(int defect)
	{
		var harness = new Harness();
		var index = Identifier();
		switch (defect)
		{
			case 0: index["key"] = new BsonDocument("other", 1); break;
			case 1: index["unique"] = false; break;
			case 2: index["collation"] = new BsonDocument("locale", "en"); break;
			case 3: index["sparse"] = true; break;
			case 4: index["partialFilterExpression"] = new BsonDocument("status", 0); break;
		}
		A.CallTo(() => harness.Indexes.ListAsync(A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult(Cursor([index])));
		await using var store = harness.CreateStore();
		await Should.ThrowAsync<InvalidOperationException>(async () => await store.StageMessageAsync(Row(), CancellationToken.None));
		harness.AssertNoMutation();
	}

	private static OutboundMessage Row() => new("test", [1], "destination") { Id = "row" };
	private static BsonDocument Description(bool safe) => new()
	{
		{ "name", "actual_collection" }, { "type", "collection" },
		{ "options", safe ? new BsonDocument() : new BsonDocument("collation", new BsonDocument { { "locale", "en" }, { "strength", 2 } }) },
	};
	private static BsonDocument Identifier() => new() { { "name", "_id_" }, { "key", new BsonDocument("_id", 1) } };
	private static IAsyncCursor<BsonDocument> Cursor(BsonDocument[] rows)
	{
		var cursor = A.Fake<IAsyncCursor<BsonDocument>>();
		A.CallTo(() => cursor.Current).Returns(rows);
		var moved = false;
		A.CallTo(() => cursor.MoveNextAsync(A<CancellationToken>._)).ReturnsLazily(() =>
		{
			var next = !moved;
			moved = true;
			return Task.FromResult(next);
		});
		return cursor;
	}

	private sealed class Harness
	{
		internal IMongoClient Client { get; } = A.Fake<IMongoClient>();
		internal IMongoDatabase Database { get; } = A.Fake<IMongoDatabase>();
		internal IMongoCollection<MongoDbOutboxDocument> Collection { get; } = A.Fake<IMongoCollection<MongoDbOutboxDocument>>();
		internal IMongoIndexManager<MongoDbOutboxDocument> Indexes { get; } = A.Fake<IMongoIndexManager<MongoDbOutboxDocument>>();
		internal Harness()
		{
			A.CallTo(() => Client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(Database);
			A.CallTo(() => Database.GetCollection<MongoDbOutboxDocument>(A<string>._, A<MongoCollectionSettings>._)).Returns(Collection);
			A.CallTo(() => Collection.Database).Returns(Database);
			A.CallTo(() => Collection.CollectionNamespace).Returns(new CollectionNamespace(new DatabaseNamespace("actual_db"), "actual_collection"));
			A.CallTo(() => Collection.Indexes).Returns(Indexes);
			A.CallTo(() => Database.ListCollectionsAsync(A<ListCollectionsOptions>._, A<CancellationToken>._))
				.ReturnsLazily(() => Task.FromResult(Cursor([Description(true)])));
			A.CallTo(() => Indexes.ListAsync(A<CancellationToken>._)).ReturnsLazily(() => Task.FromResult(Cursor([Identifier()])));
			A.CallTo(() => Indexes.CreateManyAsync(A<IEnumerable<CreateIndexModel<MongoDbOutboxDocument>>>._, A<CancellationToken>._))
				.ReturnsLazily(() => Task.FromResult<IEnumerable<string>>(["created"]));
			A.CallTo(() => Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._)).Returns(Task.CompletedTask);
		}
		internal MongoDbOutboxStore CreateStore() => new(Client, Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = "mongodb://localhost:27017", DatabaseName = "actual_db", CollectionName = "actual_collection",
			SentMessageTtlSeconds = 0,
		}), A.Fake<ILogger<MongoDbOutboxStore>>());
		internal void AssertNoMutation()
		{
			A.CallTo(() => Indexes.CreateManyAsync(A<IEnumerable<CreateIndexModel<MongoDbOutboxDocument>>>._, A<CancellationToken>._)).MustNotHaveHappened();
			A.CallTo(() => Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
		}
	}
}
