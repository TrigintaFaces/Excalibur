// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json.Serialization;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.MongoDB;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Outbox;

[Trait("Category", "Unit")]
[Trait("Component", "MongoDB")]
[Trait("Pattern", "Ownership")]
public sealed class MongoDbOutboxPayloadTransformHandoffShould
{
    private static readonly string[] CreatedIndexes = ["created"];
    private static readonly byte[] CanonicalWire = Encoding.UTF8.GetBytes("{\"secret\":\"secret\"}");

    [Fact]
    public void ProveDocumentMappingDoesNotSupplyTheEnqueueHandoffCopy()
    {
        var result = new byte[] { 0xE7, 0, 0xFF };
        var envelope = new OutboundMessage("event", result, "destination");
        var document = MongoDbOutboxDocument.FromOutboundMessage(envelope);
        document.Payload.ShouldBeSameAs(result);
        result[0] = 0;
        document.Payload[0].ShouldBe((byte)0);
    }

    [Fact]
    public async Task CopyReturnedPayloadBeforeTheMongoInsertBoundary()
    {
        var harness = new Harness();
        var result = new byte[] { 0xE7, 0, 0xFF, 0x10 };
        var expected = result.ToArray();
        var armed = false;
        var inserts = 0;
        byte[]? observed = null;
        A.CallTo(() => harness.Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._))
            .ReturnsLazily((MongoDbOutboxDocument document, InsertOneOptions options, CancellationToken token) =>
            {
                armed.ShouldBeTrue("mutation occurs after the callback's handoff, not during its synchronous copy");
                inserts++;
                result[0] = 0;
                observed = document.Payload.ToArray();
                document.Id.ShouldNotBe("mongo-valid-context-id");
                document.TenantId.ShouldBe("mongo-handoff-tenant");
                token.ShouldBe(CancellationToken.None);
                return Task.CompletedTask;
            });
        await using var store = harness.CreateStore();
        var facet = ((IOutboxStore)store).GetService(typeof(IOutboxPayloadTransformStore))
            .ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new HandoffMessage("secret");
        var context = new MessageContext(message, services) { MessageId = "mongo-valid-context-id" };
        context.GetOrCreateIdentityFeature().TenantId = "mongo-handoff-tenant";
        var callbacks = 0;
        await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) =>
        {
            callbacks++;
            wire.Length.ShouldBeGreaterThan(0);
            tenant.ShouldBe("mongo-handoff-tenant");
            token.ShouldBe(CancellationToken.None);
            armed = true;
            return ValueTask.FromResult(result);
        }, CancellationToken.None).ConfigureAwait(false);
        callbacks.ShouldBe(1);
        inserts.ShouldBe(1);
        result[0].ShouldBe((byte)0, "the post-handoff mutation branch really ran");
        observed.ShouldNotBeNull().ShouldBe(expected, "provider output ownership must precede BSON document insertion");
        await store.DisposeAsync().ConfigureAwait(false);
        A.CallTo(() => harness.Client.Dispose()).MustNotHaveHappened();
        harness.Client.Dispose();
        A.CallTo(() => harness.Client.Dispose()).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreezeTheOwningEnvelopeAndBorrowedCanonicalInputBeforeAwaitingTransform(bool tenanted)
    {
        var harness = new Harness();
        await using var store = harness.CreateStore();
        var facet = Facet(store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new HandoffMessage("secret");
        var context = Context(message, services, tenanted);
        using var supplied = new CancellationTokenSource();
        var callbacks = 0;
        var result = new byte[] { 0xE7, 0, 0xFF };
        async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> wire, string? tenant, CancellationToken token)
        {
            callbacks++;
            wire.ToArray().ShouldBe(CanonicalWire);
            tenant.ShouldBe(tenanted ? "mongo-before-tenant" : null);
            token.ShouldBe(supplied.Token);
            context.CorrelationId = "changed-correlation";
            context.CausationId = "changed-cause";
            context.GetOrCreateIdentityFeature().TenantId = "changed-tenant";
            context.SetDestination("changed-destination");
            context.SetPropagatedItem("custom", "changed");
            await Task.Yield();
            wire.ToArray().ShouldBe(CanonicalWire, "borrow is stable inside its callback lifetime");
            return result;
        }
        await facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).ConfigureAwait(false);
        callbacks.ShouldBe(1);
        var document = harness.Accepted.ShouldHaveSingleItem();
        document.Payload.ShouldBe(result);
        document.CorrelationId.ShouldBe("mongo-before-correlation");
        document.CausationId.ShouldBe("mongo-before-cause");
        document.TenantId.ShouldBe(tenanted ? "mongo-before-tenant" : "__untenanted__");
        document.Destination.ShouldBe("mongo-before-destination");
        document.Headers.Count.ShouldBe(1);
        document.Headers["custom"].ShouldBe("before");
        document.Headers.ContainsKey("request-only").ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeTheTransformOnceAndAcceptEmptyOutput()
    {
        var harness = new Harness();
        await using var store = harness.CreateStore();
        var facet = Facet(store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new HandoffMessage("secret");
        var context = Context(message, services, tenanted: true);
        var callbacks = 0;
        await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) =>
        {
            callbacks++;
            wire.ToArray().ShouldBe(CanonicalWire);
            return ValueTask.FromResult(Array.Empty<byte>());
        }, CancellationToken.None).ConfigureAwait(false);
        callbacks.ShouldBe(1);
        harness.Accepted.ShouldHaveSingleItem().Payload.ShouldBeEmpty();
    }

    [Fact]
    public async Task RejectNullCallbackBeforeSerializationOrSchemaAdmission()
    {
        var harness = new Harness();
        await using var store = harness.CreateStore();
        var facet = Facet(store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new CountingMessage();
        var context = Context(message, services, tenanted: true);
        await Should.ThrowAsync<ArgumentNullException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, null!, CancellationToken.None).AsTask());
        message.GetterCalls.ShouldBe(0);
        harness.AssertNoAdmissionOrInsert();
    }

    [Fact]
    public async Task RefusePreCancellationBeforeSerializationCallbackOrSchemaAdmission()
    {
        var harness = new Harness();
        await using var store = harness.CreateStore();
        var facet = Facet(store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new CountingMessage();
        var context = Context(message, services, tenanted: true);
        using var supplied = new CancellationTokenSource();
        supplied.Cancel();
        var callbacks = 0;
        var error = await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) =>
        {
            callbacks++;
            return ValueTask.FromResult(Array.Empty<byte>());
        }, supplied.Token).AsTask());
        error.CancellationToken.ShouldBe(supplied.Token);
        callbacks.ShouldBe(0);
        message.GetterCalls.ShouldBe(0);
        harness.AssertNoAdmissionOrInsert();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task PreserveCallbackFailureAndKeepAdmissionHealthyWithoutAnyPrematureDatabaseWork(int denial)
    {
        var harness = new Harness();
        await using var store = harness.CreateStore();
        var facet = Facet(store);
        using var services = new ServiceCollection().BuildServiceProvider();
        var message = new HandoffMessage("secret");
        var context = Context(message, services, tenanted: true);
        using var supplied = new CancellationTokenSource();
        var primary = new IOException("mongo-callback-primary");
        var callbacks = 0;
        ValueTask<byte[]> Transform(ReadOnlyMemory<byte> wire, string? tenant, CancellationToken token)
        {
            callbacks++;
            wire.ToArray().ShouldBe(CanonicalWire);
            token.ShouldBe(supplied.Token);
            if (denial == 0) { throw primary; }
            if (denial == 1) { return ValueTask.FromResult<byte[]>(null!); }
            if (denial == 2) { throw new OperationCanceledException(token); }
            supplied.Cancel();
            return ValueTask.FromResult(CanonicalWire.ToArray());
        }
        if (denial == 0)
        {
            var error = await Should.ThrowAsync<IOException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).AsTask());
            error.ShouldBeSameAs(primary);
        }
        else if (denial == 1)
        {
            await Should.ThrowAsync<InvalidOperationException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).AsTask());
        }
        else
        {
            var error = await Should.ThrowAsync<OperationCanceledException>(() => facet.EnqueueWithPayloadTransformAsync(message, context, Transform, supplied.Token).AsTask());
            error.CancellationToken.ShouldBe(supplied.Token);
        }
        callbacks.ShouldBe(1);
        harness.AssertNoAdmissionOrInsert();
        await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, token) => ValueTask.FromResult(wire.ToArray()), CancellationToken.None).ConfigureAwait(false);
        harness.Accepted.ShouldHaveSingleItem().Payload.ShouldBe(CanonicalWire);
        A.CallTo(() => harness.Database.ListCollectionsAsync(A<ListCollectionsOptions>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private static IOutboxPayloadTransformStore Facet(IOutboxStore store) =>
        store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();

    private static MessageContext Context(IDispatchMessage message, IServiceProvider services, bool tenanted)
    {
        var context = new MessageContext(message, services)
        {
            MessageId = "mongo-valid-context-id", CorrelationId = "mongo-before-correlation", CausationId = "mongo-before-cause",
        };
        if (tenanted) { context.GetOrCreateIdentityFeature().TenantId = "mongo-before-tenant"; }
        context.SetDestination("mongo-before-destination");
        context.SetPropagatedItem("custom", "before");
        context.Items["request-only"] = "not-propagated";
        return context;
    }

    private static IAsyncCursor<BsonDocument> Cursor(BsonDocument[] documents)
    {
        var cursor = A.Fake<IAsyncCursor<BsonDocument>>();
        A.CallTo(() => cursor.Current).Returns(documents);
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
        internal List<MongoDbOutboxDocument> Accepted { get; } = [];
        internal IMongoClient Client { get; } = A.Fake<IMongoClient>();
        internal IMongoDatabase Database { get; } = A.Fake<IMongoDatabase>();
        internal IMongoCollection<MongoDbOutboxDocument> Collection { get; } = A.Fake<IMongoCollection<MongoDbOutboxDocument>>();
        internal IMongoIndexManager<MongoDbOutboxDocument> Indexes { get; } = A.Fake<IMongoIndexManager<MongoDbOutboxDocument>>();

        internal Harness()
        {
            A.CallTo(() => Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._))
                .ReturnsLazily((MongoDbOutboxDocument document, InsertOneOptions options, CancellationToken token) =>
                {
                    Accepted.Add(document);
                    return Task.CompletedTask;
                });
            A.CallTo(() => Client.GetDatabase(A<string>._, A<MongoDatabaseSettings>._)).Returns(Database);
            A.CallTo(() => Database.GetCollection<MongoDbOutboxDocument>(A<string>._, A<MongoCollectionSettings>._)).Returns(Collection);
            A.CallTo(() => Collection.Database).Returns(Database);
            A.CallTo(() => Collection.CollectionNamespace).Returns(new CollectionNamespace(new DatabaseNamespace("actual_db"), "actual_collection"));
            A.CallTo(() => Collection.Indexes).Returns(Indexes);
            A.CallTo(() => Database.ListCollectionsAsync(A<ListCollectionsOptions>._, A<CancellationToken>._))
                .ReturnsLazily(() => Task.FromResult(Cursor([new BsonDocument
                {
                    { "name", "actual_collection" }, { "type", "collection" }, { "options", new BsonDocument() },
                }])));
            A.CallTo(() => Indexes.ListAsync(A<CancellationToken>._))
                .ReturnsLazily(() => Task.FromResult(Cursor([new BsonDocument
                {
                    { "name", "_id_" }, { "key", new BsonDocument("_id", 1) },
                }])));
            A.CallTo(() => Indexes.CreateManyAsync(A<IEnumerable<CreateIndexModel<MongoDbOutboxDocument>>>._, A<CancellationToken>._))
                .Returns(Task.FromResult<IEnumerable<string>>(CreatedIndexes));
        }

        internal void AssertNoAdmissionOrInsert()
        {
            Accepted.ShouldBeEmpty();
            A.CallTo(() => Database.ListCollectionsAsync(A<ListCollectionsOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => Database.CreateCollectionAsync(A<string>._, A<CreateCollectionOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => Indexes.CreateManyAsync(A<IEnumerable<CreateIndexModel<MongoDbOutboxDocument>>>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => Collection.InsertOneAsync(A<MongoDbOutboxDocument>._, A<InsertOneOptions>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        internal MongoDbOutboxStore CreateStore() => new(Client, Options.Create(new MongoDbOutboxOptions
        {
            ConnectionString = "mongodb://localhost:27017", DatabaseName = "actual_db", CollectionName = "actual_collection",
            SentMessageTtlSeconds = 0,
        }), NullLogger<MongoDbOutboxStore>.Instance);
    }

    private sealed class CountingMessage : IDispatchMessage
    {
        public string Secret { get { GetterCalls++; return "counted-secret"; } }
        [JsonIgnore]
        public int GetterCalls { get; private set; }
    }

    private sealed record HandoffMessage(string Secret) : IDispatchMessage;
}
