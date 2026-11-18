// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json.Serialization;
using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Outbox.MongoDB;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxLegacyEnqueueTransformShould(MongoDbOutboxStoreContainerFixture fixture)
{
    private static readonly byte[] CanonicalWire = Encoding.UTF8.GetBytes("{\"wire_name\":\"mongo-secret\",\"state\":\"Ready\"}");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PreserveCanonicalPayloadAndOwningBsonEnvelopeThroughOrdinaryEnqueue(bool explicitRouting) =>
        WithMongoAsync(async (store, collection, token) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("mongo-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting);
            await store.EnqueueAsync(message, context, token).ConfigureAwait(false);
            var row = (await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(token).ConfigureAwait(false)).ShouldHaveSingleItem();
            AssertEnvelope(row, context, explicitRouting);
            row["payload"].IsBsonBinaryData.ShouldBeTrue();
            row["payload"].AsBsonBinaryData.Bytes.ShouldBe(CanonicalWire);
            row["createdAt"].BsonType.ShouldBe(BsonType.DateTime);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.Id.ShouldBe(row["_id"].AsString);
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            var sent = await collection.Find(new BsonDocument("_id", receipt.Id)).SingleAsync(token).ConfigureAwait(false);
            sent["sentAt"].BsonType.ShouldBe(BsonType.DateTime);
            using var cursor = await collection.Indexes.ListAsync(token).ConfigureAwait(false);
            var indexes = await cursor.ToListAsync(token).ConfigureAwait(false);
            var ttl = indexes.Single(index => index["key"].AsBsonDocument.Contains("sentAt"));
            ttl["expireAfterSeconds"].ToInt64().ShouldBe(600);
            sent["payload"].AsBsonBinaryData.Bytes.ShouldBe(CanonicalWire);
        });

    [Theory]
    [InlineData(EncryptionMode.EncryptAndDecrypt)]
    [InlineData(EncryptionMode.EncryptNewDecryptAll)]
    public Task EncryptTheOwningCanonicalPayloadWithoutAcceptingPlaintext(EncryptionMode mode) =>
        WithMongoAsync(async (raw, collection, token) =>
        {
            await WithEncryptionHostAsync(raw, mode, async host =>
            {
                var store = host.Services.GetRequiredKeyedService<IOutboxStore>("mongo-transform-control");
                store.ShouldNotBeSameAs(raw);
                host.Services.GetRequiredService<IKeyManagementProvider>().ShouldBeOfType<InMemoryKeyManagementProvider>();
                host.Services.GetRequiredService<IEncryptionProviderRegistry>().GetPrimary().ShouldBeOfType<AesGcmEncryptionProvider>();
                var message = new WireMessage("mongo-secret", WireState.Ready, null);
                var context = NewContext(message, host.Services, explicitRouting: true);
                try
                {
                    await store.EnqueueAsync(message, context, token).ConfigureAwait(false);
                }
                catch (InvalidOperationException error) when (error.Message.StartsWith(
                    "Encrypted legacy EnqueueAsync requires the owning outbox provider's IOutboxPayloadTransformStore capability.",
                    StringComparison.Ordinal))
                {
                    // Only this known pre-provider refusal guarantees no accepting Mongo insert.
                    try
                    {
                        using var inventory = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        (await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: inventory.Token).ConfigureAwait(false)).ShouldBe(0);
                    }
                    catch (Exception inventoryError)
                    {
                        error.Data["SecondaryNoAcceptInventory"] = inventoryError;
                    }
                    throw;
                }
                var row = (await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(token).ConfigureAwait(false)).ShouldHaveSingleItem();
                AssertEnvelope(row, context, explicitRouting: true);
                var ciphertext = row["payload"].AsBsonBinaryData.Bytes.ToArray();
                EncryptedData.IsFieldEncrypted(ciphertext).ShouldBeTrue();
                var receipt = (await raw.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
                var completion = ((IOutboxStore)raw).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
                (await completion.MarkFailedAsync(receipt.Id, "decrypt-control", 1, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
                var admin = store.GetService(typeof(IOutboxStoreAdmin)).ShouldBeAssignableTo<IOutboxStoreAdmin>().ShouldNotBeNull();
                var decoded = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
                decoded.Id.ShouldBe(row["_id"].AsString);
                decoded.Payload.ShouldBe(CanonicalWire);
                var unchanged = await collection.Find(new BsonDocument("_id", decoded.Id)).SingleAsync(token).ConfigureAwait(false);
                unchanged["payload"].AsBsonBinaryData.Bytes.ShouldBe(ciphertext);
                message.Secret.ShouldBe("mongo-secret");
            }).ConfigureAwait(false);
        });

    [Fact]
    public Task FreezeThePreparedEnvelopeWhileBorrowingCanonicalInputAndKeepStoredOutputOwned() =>
        WithMongoAsync(async (store, collection, token) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("mongo-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            var output = new byte[] { 0xE7, 0, 0xFF };
            var expected = output.ToArray();
            var callbacks = 0;
            async ValueTask<byte[]> Transform(ReadOnlyMemory<byte> wire, string? tenant, CancellationToken supplied)
            {
                callbacks++;
                wire.ToArray().ShouldBe(CanonicalWire);
                tenant.ShouldBe("mongo-tenant");
                supplied.ShouldBe(token);
                context.CorrelationId = "changed-correlation";
                context.CausationId = "changed-cause";
                context.GetOrCreateIdentityFeature().TenantId = "changed-tenant";
                context.SetDestination("changed-destination");
                context.SetPropagatedItem("custom", "changed");
                await Task.Yield();
                wire.ToArray().ShouldBe(CanonicalWire, "borrow remains stable inside callback lifetime");
                return output;
            }
            await facet.EnqueueWithPayloadTransformAsync(message, context, Transform, token).ConfigureAwait(false);
            callbacks.ShouldBe(1);
            output[0] = 0;
            var row = (await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(token).ConfigureAwait(false)).ShouldHaveSingleItem();
            AssertEnvelope(row, context, explicitRouting: true);
            row["payload"].AsBsonBinaryData.Bytes.ShouldBe(expected);
            await store.DisposeAsync().ConfigureAwait(false);
            var borrowed = collection.Database.Client;
            var ownerCollection = borrowed.GetDatabase(collection.CollectionNamespace.DatabaseNamespace.DatabaseName)
                .GetCollection<BsonDocument>(collection.CollectionNamespace.CollectionName);
            (await ownerCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: token).ConfigureAwait(false)).ShouldBe(1,
                "disposing the provider must leave its caller-owned MongoClient usable");
        });

    [Fact]
    public Task PersistEmptyTransformedPayloadAsBsonBinaryRatherThanMissingOrNull() =>
        WithMongoAsync(async (store, collection, token) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("mongo-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            var callbacks = 0;
            await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, supplied) =>
            {
                callbacks++;
                wire.ToArray().ShouldBe(CanonicalWire);
                return ValueTask.FromResult(Array.Empty<byte>());
            }, token).ConfigureAwait(false);
            callbacks.ShouldBe(1);
            var row = (await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(token).ConfigureAwait(false)).ShouldHaveSingleItem();
            row.Contains("payload").ShouldBeTrue();
            row["payload"].IsBsonBinaryData.ShouldBeTrue();
            row["payload"].AsBsonBinaryData.Bytes.ShouldBeEmpty();
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task RefuseCallbackFailureBeforeCollectionCreationAndKeepHealthyReceiptProgress(int denial) =>
        WithMongoAsync(async (store, collection, token) =>
        {
            var facet = Facet(store);
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("mongo-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            using var supplied = new CancellationTokenSource();
            var primary = new IOException("mongo-real-callback-primary");
            var callbacks = 0;
            ValueTask<byte[]> Transform(ReadOnlyMemory<byte> wire, string? tenant, CancellationToken callbackToken)
            {
                callbacks++;
                wire.ToArray().ShouldBe(CanonicalWire);
                callbackToken.ShouldBe(supplied.Token);
                if (denial == 0) { throw primary; }
                if (denial == 1) { return ValueTask.FromResult<byte[]>(null!); }
                if (denial == 2) { throw new OperationCanceledException(callbackToken); }
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
            (await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: token).ConfigureAwait(false)).ShouldBe(0);
            using (var cursor = await collection.Database.ListCollectionsAsync(new ListCollectionsOptions
            {
                Filter = new BsonDocument("name", collection.CollectionNamespace.CollectionName),
            }, token).ConfigureAwait(false))
            {
                (await cursor.ToListAsync(token).ConfigureAwait(false)).ShouldBeEmpty("known callback denial must precede schema admission/index creation");
            }
            await facet.EnqueueWithPayloadTransformAsync(message, context, (wire, tenant, callbackToken) => ValueTask.FromResult(wire.ToArray()), token).ConfigureAwait(false);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            var healthy = await collection.Find(new BsonDocument("_id", receipt.Id)).SingleAsync(token).ConfigureAwait(false);
            healthy["payload"].AsBsonBinaryData.Bytes.ShouldBe(CanonicalWire);
            healthy["status"].AsInt32.ShouldBe((int)OutboxStatus.Sent);
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("mongo-external-context-id")]
    public Task KeepUnusedContextIdentityIndependentFromTheFreshMongoRowAndCompletion(string? contextId) =>
        WithMongoAsync(async (store, collection, token) =>
        {
            using var services = new ServiceCollection().BuildServiceProvider();
            var message = new WireMessage("mongo-secret", WireState.Ready, null);
            var context = NewContext(message, services, explicitRouting: true);
            context.MessageId = contextId!;
            await store.EnqueueAsync(message, context, token).ConfigureAwait(false);
            var row = (await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(token).ConfigureAwait(false)).ShouldHaveSingleItem();
            AssertEnvelope(row, context, explicitRouting: true);
            row["payload"].AsBsonBinaryData.Bytes.ShouldBe(CanonicalWire);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.Id.ShouldBe(row["_id"].AsString);
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
        });

    private static IOutboxPayloadTransformStore Facet(IOutboxStore store) =>
        store.GetService(typeof(IOutboxPayloadTransformStore)).ShouldBeAssignableTo<IOutboxPayloadTransformStore>().ShouldNotBeNull();

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public Task PreserveStagedSequenceInt64ThroughRealBsonClaimAndCompletion(long sequence) =>
        WithMongoAsync(async (store, collection, token) =>
        {
            var message = new OutboundMessage("sequence-control", CanonicalWire, "destination")
            {
                PartitionKey = "sequence-partition", SequenceNumber = sequence,
            };
            await store.StageMessageAsync(message, token).ConfigureAwait(false);
            var raw = (await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(token).ConfigureAwait(false)).ShouldHaveSingleItem();
            raw["sequenceNumber"].BsonType.ShouldBe(BsonType.Int64);
            raw["sequenceNumber"].AsInt64.ShouldBe(sequence);
            var receipt = (await store.GetUnsentMessagesAsync(10, token).ConfigureAwait(false)).ShouldHaveSingleItem();
            receipt.SequenceNumber.ShouldBe(sequence);
            receipt.PartitionKey.ShouldBe("sequence-partition");
            var completion = ((IOutboxStore)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>().ShouldNotBeNull();
            (await completion.MarkSentAsync(receipt.Id, receipt.DispatcherId!, token).ConfigureAwait(false)).ShouldBe(OutboxCompletionOutcome.Applied);
            var sent = await collection.Find(new BsonDocument("_id", receipt.Id)).SingleAsync(token).ConfigureAwait(false);
            sent["sequenceNumber"].BsonType.ShouldBe(BsonType.Int64);
            sent["sequenceNumber"].AsInt64.ShouldBe(sequence);
            sent["payload"].AsBsonBinaryData.Bytes.ShouldBe(CanonicalWire);
        });

    private async Task WithMongoAsync(Func<MongoDbOutboxStore, IMongoCollection<BsonDocument>, CancellationToken, Task> assertion)
    {
        fixture.DockerAvailable.ShouldBeTrue("Mongo payload and BSON controls require actual standalone MongoDB; never skipped");
        var name = "enqueue_transform_" + Guid.NewGuid().ToString("N");
        MongoClient? client = null;
        IMongoDatabase? database = null;
        MongoDbOutboxStore? store = null;
        Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            client = new MongoClient(fixture.ConnectionString);
            database = client.GetDatabase(fixture.DatabaseName);
            var collection = database.GetCollection<BsonDocument>(name);
            store = new MongoDbOutboxStore(client, Options.Create(new MongoDbOutboxOptions
            {
                ConnectionString = fixture.ConnectionString, DatabaseName = fixture.DatabaseName,
                CollectionName = name, SentMessageTtlSeconds = 600, LeaseTimeoutSeconds = 120, FailureBackoffFloorSeconds = 1,
            }), NullLogger<MongoDbOutboxStore>.Instance);
            using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await assertion(store, collection, operation.Token).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            if (database is not null)
            {
                try
                {
                    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await database.DropCollectionAsync(name, stop.Token).ConfigureAwait(false);
                }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (store is not null)
            {
                try { await store.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { cleanup.Add(error); }
            }
            if (client is not null)
            {
                try { client.Dispose(); }
                catch (Exception error) { cleanup.Add(error); }
            }
        }
        Rethrow(primary, cleanup);
    }

    private static async Task WithEncryptionHostAsync(MongoDbOutboxStore raw, EncryptionMode mode, Func<IHost, Task> assertion)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddDevEncryption();
        builder.Services.Configure<EncryptionOptions>(options => options.Mode = mode);
        builder.Services.AddKeyedSingleton<IOutboxStore>("mongo-transform-control", raw);
        builder.Services.AddOutboxEncryption();
        var host = builder.Build();
        Exception? primary = null;
        var cleanup = new List<Exception>();
        try
        {
            using var start = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await host.StartAsync(start.Token).ConfigureAwait(false);
            await assertion(host).ConfigureAwait(false);
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await host.StopAsync(stop.Token).ConfigureAwait(false);
            }
            catch (Exception error) { cleanup.Add(error); }
            try
            {
                if (host is IAsyncDisposable asyncHost) { await asyncHost.DisposeAsync().ConfigureAwait(false); }
                else { host.Dispose(); }
            }
            catch (Exception error) { cleanup.Add(error); }
        }
        Rethrow(primary, cleanup);
    }

    private static void Rethrow(Exception? primary, List<Exception> cleanup)
    {
        foreach (var error in cleanup)
        {
            if (primary is null) { primary = error; }
            else { primary.Data["SecondaryMongoCleanup" + primary.Data.Count] = error; }
        }
        if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
    }

    private static MessageContext NewContext(WireMessage message, IServiceProvider services, bool explicitRouting)
    {
        var context = new MessageContext(message, services)
        {
            MessageId = "mongo-valid-context-id", CorrelationId = "mongo-correlation", CausationId = "mongo-cause",
        };
        context.SetPropagatedItem("custom", "before");
        if (explicitRouting)
        {
            context.GetOrCreateIdentityFeature().TenantId = "mongo-tenant";
            context.SetDestination("mongo-destination");
        }
        return context;
    }

    private static void AssertEnvelope(BsonDocument row, MessageContext context, bool explicitRouting)
    {
        row["_id"].IsString.ShouldBeTrue();
        Guid.TryParse(row["_id"].AsString, out var parsed).ShouldBeTrue();
        parsed.ShouldNotBe(Guid.Empty);
        row["_id"].AsString.ShouldNotBe(context.MessageId);
        row["messageType"].AsString.ShouldBe(typeof(WireMessage).FullName);
        row["destination"].AsString.ShouldBe(explicitRouting ? "mongo-destination" : typeof(WireMessage).FullName);
        row["correlationId"].AsString.ShouldBe("mongo-correlation");
        row["causationId"].AsString.ShouldBe("mongo-cause");
        row["tenantId"].AsString.ShouldBe(explicitRouting ? "mongo-tenant" : "__untenanted__");
        row["headers"].AsBsonDocument["custom"].AsString.ShouldBe("before");
    }

    private enum WireState { Ready }
    private sealed record WireMessage([property: JsonPropertyName("wire_name")] string Secret, WireState State, string? Optional) : IDispatchMessage;
}
