// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Excalibur.Outbox.Redis;
using StackExchange.Redis;
using Tests.Shared.Fixtures;
namespace Excalibur.Integration.Tests.Redis.Outbox;
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Concurrency")]
public sealed class RedisOutboxCountedRetirementShould(RedisContainerFixture fixture)
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

    private async Task<Stores> OpenAsync()
    {
        fixture.DockerAvailable.ShouldBeTrue("Counted retirement must execute against Redis, never skip.");
        var prefix = "counted-" + Guid.NewGuid().ToString("N");
        var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        RedisOutboxStore Create(string owner) => new(connection, Options.Create(new RedisOutboxOptions
        {
            ConnectionString = fixture.ConnectionString, KeyPrefix = prefix, ProcessorId = owner,
        }), NullLogger<RedisOutboxStore>.Instance);
        return new Stores(Create("counted-A"), Create("counted-B"), connection, prefix);
    }
    private sealed class Stores(RedisOutboxStore owner, RedisOutboxStore peer, ConnectionMultiplexer connection, string prefix) : IAsyncDisposable
    {
        internal IOutboxStore Owner => owner;
        internal IOutboxStore Peer => peer;
        internal async Task<Snapshot?> ReadOptionalAsync(string id)
        {
            var row = await connection.GetDatabase().HashGetAsync(prefix + ":msg:" + id, ["Status", "RetryCount", "LastError"]);
            return row[0].IsNull ? null : new Snapshot((int)row[0], (int)row[1], row[2].IsNull ? null : row[2].ToString());
        }
        internal async Task<Snapshot> ReadAsync(string id) => (await ReadOptionalAsync(id)).ShouldNotBeNull();
        public async ValueTask DisposeAsync()
        {
            await owner.DisposeAsync(); await peer.DisposeAsync();
            var server = connection.GetServer(connection.GetEndPoints()[0]);
            foreach (var key in server.Keys(pattern: prefix + ":*")) await connection.GetDatabase().KeyDeleteAsync(key);
            await connection.DisposeAsync();
        }
    }
}
