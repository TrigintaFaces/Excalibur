// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Integration.Tests.Data.Outbox;
using Excalibur.Outbox.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Excalibur.Integration.Tests.Redis.Outbox;

[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "Outbox")]
[Trait("Database", "Redis")]
public sealed class RedisOutboxStrongCompletionShould(RedisContainerFixture fixture)
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task KeepConfiguredProcessPrefixesDistinctOnLegacyRoutes(int operation)
    {
        fixture.DockerAvailable.ShouldBeTrue("legacy process protection must be verified against actual Lua, never skipped");
        await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var prefix = "strong-prefix-" + Guid.NewGuid().ToString("N");
        RedisOutboxOptions Configure(string owner) => new()
        {
            ConnectionString = fixture.ConnectionString, KeyPrefix = prefix, ProcessorId = owner,
            LeaseTimeoutSeconds = 120, FailureBackoffFloorSeconds = 1,
        };
        await using var owner = new RedisOutboxStore(connection, Options.Create(Configure("A:peer")), NullLogger<RedisOutboxStore>.Instance);
        await using var peer = new RedisOutboxStore(connection, Options.Create(Configure("A")), NullLogger<RedisOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1], "destination");
        await owner.StageMessageAsync(row, CancellationToken.None);
        var claim = (await owner.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();
        if (operation == 0) { await peer.MarkFailedAsync(row.Id, "not owner", 99, CancellationToken.None); }
        else if (operation == 1) { await peer.MarkFailedWithBackoffAsync(row.Id, "not owner", 99, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None); }
        else { (await peer.MarkDeadLetteredAsync(row.Id, "not owner", 99, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.ClaimLost); }
        (await Complete(owner, row.Id, claim.DispatcherId!, 0, CancellationToken.None)).ShouldBe(OutboxCompletionOutcome.Applied);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task RefuseSameProcessOldAcquisitionAndPreserveSuccessorProgress(int operation)
    {
        fixture.DockerAvailable.ShouldBeTrue("claim completion requires actual Lua/server-clock verification, never skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var prefix = "strong-claim-" + Guid.NewGuid().ToString("N");
        await using var store = new RedisOutboxStore(connection, Options.Create(new RedisOutboxOptions
        {
            ConnectionString = fixture.ConnectionString, KeyPrefix = prefix, ProcessorId = "same-process",
            LeaseTimeoutSeconds = 1, FailureBackoffFloorSeconds = 1, SentMessageTtlSeconds = 300,
        }), NullLogger<RedisOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1, 2], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        var first = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        first.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        var evidence = first.DispatcherId!;
        // delay-ok: the actual server lease must elapse; this is the expiry/reclaim behavior under test.
        await Task.Delay(TimeSpan.FromMilliseconds(1200), deadline.Token); // delay-ok: The actual Redis server lease must elapse; explicit stale/current claim and persisted outcomes verify the transition.
        var second = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        second.DispatcherId.ShouldNotBe(evidence);
        first.DispatcherId.ShouldBe(evidence);
        (await Complete(store, row.Id, evidence, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, row.Id, second.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        var repeatedOutcome = operation is 0 or 3 ? OutboxCompletionOutcome.AlreadyTerminal : OutboxCompletionOutcome.ClaimLost;
        (await Complete(store, row.Id, evidence, operation, deadline.Token)).ShouldBe(repeatedOutcome);
        (await Complete(store, row.Id, second.DispatcherId!, operation, deadline.Token)).ShouldBe(repeatedOutcome);
        await AssertPersistedCompletion(connection.GetDatabase(), prefix, row.Id, operation);
        if (operation == 0)
        {
            var ttl = await connection.GetDatabase().KeyTimeToLiveAsync(prefix + ":msg:" + row.Id);
            ttl.ShouldNotBeNull();
            ttl.Value.ShouldBeGreaterThan(TimeSpan.Zero);
            ttl.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(300));
        }
        if (operation is 0 or 3)
        {
            (await store.GetUnsentMessagesAsync(1, deadline.Token)).ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task RefuseExpiredUnreclaimedOrReleasedLeaseAndReportMissingRows(int operation)
    {
        fixture.DockerAvailable.ShouldBeTrue("claim completion requires actual Lua/server-clock verification, never skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var connection = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var prefix = "strong-expiry-" + Guid.NewGuid().ToString("N");
        await using var store = new RedisOutboxStore(connection, Options.Create(new RedisOutboxOptions
        {
            ConnectionString = fixture.ConnectionString, KeyPrefix = prefix, ProcessorId = "same-process",
            LeaseTimeoutSeconds = 1, FailureBackoffFloorSeconds = 1,
        }), NullLogger<RedisOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        (await Complete(store, row.Id, "unclaimed", operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var first = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        // delay-ok: prove expiry is refused before any other claim has cleared the old lease.
        await Task.Delay(TimeSpan.FromMilliseconds(1200), deadline.Token); // delay-ok: The actual Redis server lease must elapse; explicit stale/current claim and persisted outcomes verify the transition.
        (await Complete(store, row.Id, first.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var second = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        (await Complete(store, row.Id, second.DispatcherId!, 1, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, row.Id, second.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, "missing", "known", operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
    }

    private static async Task AssertPersistedCompletion(IDatabase database, string prefix, string id, int operation)
    {
        var key = prefix + ":msg:" + id;
        var status = operation switch
        {
            0 => OutboxStatus.Sent,
            1 => OutboxStatus.Failed,
            2 => OutboxStatus.Staged,
            _ => OutboxStatus.DeadLettered,
        };
        ((string?)await database.HashGetAsync(key, "Status")).ShouldBe(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture));
        (await database.HashExistsAsync(key, "LeasedBy")).ShouldBeFalse();
        (await database.HashExistsAsync(key, "LeasedAt")).ShouldBeFalse();
        (await database.SortedSetScoreAsync(prefix + ":idx:leased", id)).ShouldBeNull();
        if (operation != 0)
        {
            ((int)await database.HashGetAsync(key, "RetryCount")).ShouldBe(2);
            ((string?)await database.HashGetAsync(key, "LastError")).ShouldBe(operation switch { 1 => "failure", 2 => "backoff", _ => "dead" });
        }
        if (operation is 1 or 2)
        {
            var scheduled = await database.SortedSetScoreAsync(prefix + ":idx:scheduled", id);
            scheduled.ShouldNotBeNull();
            var recordedNextAttempt = (long)await database.HashGetAsync(key, "NextAttemptAt");
            scheduled.Value.ShouldBe((double)recordedNextAttempt);
            recordedNextAttempt.ShouldBeGreaterThan((long)await database.HashGetAsync(key, "LastAttemptAt"));
            if (operation == 2)
            {
                // The requested minute-long backoff must survive, not just the one-second provider floor.
                recordedNextAttempt.ShouldBeGreaterThanOrEqualTo((long)await database.HashGetAsync(key, "LastAttemptAt") + 50_000);
                (await database.SortedSetScoreAsync(prefix + ":idx:failed", id)).ShouldBeNull();
            }
            else
            {
                (await database.SortedSetScoreAsync(prefix + ":idx:failed", id)).ShouldNotBeNull();
            }
        }
        else
        {
            (await database.SortedSetScoreAsync(prefix + ":idx:scheduled", id)).ShouldBeNull();
            (await database.SortedSetScoreAsync(prefix + ":idx:failed", id)).ShouldBeNull();
        }
    }

    private static ValueTask<OutboxCompletionOutcome> Complete(RedisOutboxStore store, string id, string claim, int operation, CancellationToken token)
    {
        var strong = ((IServiceProvider)store).GetService(typeof(IClaimScopedOutboxStore)).ShouldBeAssignableTo<IClaimScopedOutboxStore>();
        return operation switch
        {
            0 => strong.MarkSentAsync(id, claim, token),
            1 => strong.MarkFailedAsync(id, "failure", 2, claim, token),
            2 => strong.MarkFailedWithBackoffAsync(id, "backoff", 2, DateTimeOffset.UtcNow.AddMinutes(1), claim, token),
            _ => ((IServiceProvider)store).GetService(typeof(IClaimScopedDeadLetterableOutboxStore)).ShouldBeAssignableTo<IClaimScopedDeadLetterableOutboxStore>()
                .MarkDeadLetteredAsync(id, "dead", 2, claim, token),
        };
    }
}
