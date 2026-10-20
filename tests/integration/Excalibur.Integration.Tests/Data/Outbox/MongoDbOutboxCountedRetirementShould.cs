// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Excalibur.Outbox.MongoDB;
using MongoDB.Bson;
using MongoDB.Driver;
namespace Excalibur.Integration.Tests.Data.Outbox;
[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Concurrency")]
public sealed class MongoDbOutboxCountedRetirementShould(MongoDbOutboxStoreContainerFixture fixture)
{

    [Theory]
    [InlineData(0, 2)]
    [InlineData(4, 1)]
    public async Task PersistTheMaximumCountInTheTerminalWrite(int existing, int supplied)
    {
        await using var stores = await OpenAsync();
        var row = new OutboundMessage("counted", [1, 2, 3], "local") { RetryCount = existing };
        await stores.Owner.StageMessageAsync(row, Ct);
        await ((IDeadLetterableOutboxStore)stores.Owner).MarkDeadLetteredAsync(row.Id, "original-reason", supplied, Ct);
        var first = await stores.ReadAsync(row.Id);
        first.ShouldBe(new Snapshot((int)OutboxStatus.DeadLettered, Math.Max(existing, supplied), "original-reason"));
        await ((IDeadLetterableOutboxStore)stores.Owner).MarkDeadLetteredAsync(row.Id, "late-reason", 99, Ct);
        (await stores.ReadAsync(row.Id)).ShouldBe(first);
    }

    [Fact]
    public async Task RefusePeerOwnedRetirementWithoutChangingCountReasonOrStatus()
    {
        await using var stores = await OpenAsync();
        var row = new OutboundMessage("peer-owned", [4, 5], "local") { RetryCount = 1 };
        await stores.Owner.StageMessageAsync(row, Ct);
        var claimed = (await stores.Peer.GetUnsentMessagesAsync(10, Ct)).ShouldHaveSingleItem();
        claimed.Id.ShouldBe(row.Id);
        var before = await stores.ReadAsync(row.Id);
        await ((IDeadLetterableOutboxStore)stores.Owner).MarkDeadLetteredAsync(row.Id, "stale-owner", 77, Ct);
        (await stores.ReadAsync(row.Id)).ShouldBe(before);
        await ((IDeadLetterableOutboxStore)stores.Peer).MarkDeadLetteredAsync(row.Id, "current-owner", 2, Ct);
        (await stores.ReadAsync(row.Id)).ShouldBe(new Snapshot((int)OutboxStatus.DeadLettered, 2, "current-owner"));
    }

    [Fact]
    public async Task RejectNegativeCountAndPreserveAlreadySentAndMissingRows()
    {
        await using var stores = await OpenAsync();
        var row = new OutboundMessage("negative", [6], "local");
        await stores.Owner.StageMessageAsync(row, Ct);
        var before = await stores.ReadAsync(row.Id);
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await ((IDeadLetterableOutboxStore)stores.Owner).MarkDeadLetteredAsync(row.Id, "invalid", -1, Ct));
        (await stores.ReadAsync(row.Id)).ShouldBe(before);
        await stores.Owner.MarkSentAsync(row.Id, Ct);
        var sent = await stores.ReadAsync(row.Id);
        sent.Status.ShouldBe((int)OutboxStatus.Sent);
        await ((IDeadLetterableOutboxStore)stores.Owner).MarkDeadLetteredAsync(row.Id, "late", 99, Ct);
        (await stores.ReadAsync(row.Id)).ShouldBe(sent);
        await ((IDeadLetterableOutboxStore)stores.Owner).MarkDeadLetteredAsync("missing-counted-row", "missing", 5, Ct);
        (await stores.ReadOptionalAsync("missing-counted-row")).ShouldBeNull();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private sealed record Snapshot(int Status, int Count, string? Reason);

    private Task<Stores> OpenAsync()
    {
        fixture.DockerAvailable.ShouldBeTrue("Counted retirement must execute against MongoDB, never skip.");
        var collection = "counted_" + Guid.NewGuid().ToString("N");
        MongoDbOutboxStore Create(string owner) => new(Options.Create(new MongoDbOutboxOptions
        {
            ConnectionString = fixture.ConnectionString, DatabaseName = fixture.DatabaseName,
            CollectionName = collection, ProcessorId = owner,
        }), NullLogger<MongoDbOutboxStore>.Instance);
        var database = new MongoClient(fixture.ConnectionString).GetDatabase(fixture.DatabaseName);
        return Task.FromResult(new Stores(Create("counted-A"), Create("counted-B"), database, collection));
    }
    private sealed class Stores(MongoDbOutboxStore owner, MongoDbOutboxStore peer, IMongoDatabase database, string collection) : IAsyncDisposable
    {
        internal IOutboxStore Owner => owner;
        internal IOutboxStore Peer => peer;
        internal async Task<Snapshot?> ReadOptionalAsync(string id)
        {
            var row = await database.GetCollection<BsonDocument>(collection).Find(new BsonDocument("_id", id)).SingleOrDefaultAsync(Ct);
            return row is null ? null : new Snapshot(row["status"].AsInt32, row["retryCount"].AsInt32,
                row.TryGetValue("lastError", out var reason) && !reason.IsBsonNull ? reason.AsString : null);
        }
        internal async Task<Snapshot> ReadAsync(string id) => (await ReadOptionalAsync(id)).ShouldNotBeNull();
        public async ValueTask DisposeAsync()
        {
            await owner.DisposeAsync(); await peer.DisposeAsync();
            await database.DropCollectionAsync(collection, CancellationToken.None);
        }
    }
}
