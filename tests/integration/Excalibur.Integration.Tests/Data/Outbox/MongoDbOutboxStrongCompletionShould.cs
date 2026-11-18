// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Outbox;

[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Data")]
[Trait("Pattern", "Outbox")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxStrongCompletionShould(MongoDbOutboxStoreContainerFixture fixture)
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ClassifyAndMutateSameLiveAcquisitionInOneRealCommand(int operation)
    {
        fixture.DockerAvailable.ShouldBeTrue("atomic preimage projection requires a real standalone server; never skipped");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MongoDbOutboxOptions
        {
            ConnectionString = fixture.ConnectionString, DatabaseName = fixture.DatabaseName,
            CollectionName = "strong_" + Guid.NewGuid().ToString("N"), ProcessorId = "same-process",
            LeaseTimeoutSeconds = 1, FailureBackoffFloorSeconds = 1,
        };
        await using var store = new MongoDbOutboxStore(Options.Create(options), NullLogger<MongoDbOutboxStore>.Instance);
        var row = new OutboundMessage("event", [1, 2], "destination");
        await store.StageMessageAsync(row, deadline.Token);
        var old = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        old.DispatcherId.ShouldNotBeNullOrWhiteSpace();
        // delay-ok: expire the actual server lease, not a local fake clock, before the same process reclaims.
        await Task.Delay(TimeSpan.FromMilliseconds(1200), deadline.Token); // delay-ok: Expire the real MongoDB server lease; stale refusal and a fresh claim independently verify the transition.
        (await Complete(store, row.Id, old.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        var current = (await store.GetUnsentMessagesAsync(1, deadline.Token)).Single();
        current.DispatcherId.ShouldNotBe(old.DispatcherId);
        (await Complete(store, row.Id, old.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, row.Id, current.DispatcherId!, operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.Applied);
        (await Complete(store, row.Id, current.DispatcherId!, operation, deadline.Token)).ShouldBe(operation is 0 or 3
            ? OutboxCompletionOutcome.AlreadyTerminal : OutboxCompletionOutcome.ClaimLost);
        (await Complete(store, "missing", "known", operation, deadline.Token)).ShouldBe(OutboxCompletionOutcome.MessageNotFound);
        var client = new MongoClient(fixture.ConnectionString);
        var collection = client.GetDatabase(options.DatabaseName).GetCollection<BsonDocument>(options.CollectionName);
        var persisted = await collection.Find(new BsonDocument("_id", row.Id)).SingleAsync(deadline.Token);
        persisted.Contains("outcome").ShouldBeFalse("the computed preimage receipt is returned only, not persisted");
        persisted["status"].AsInt32.ShouldBe((int)(operation is 0 ? OutboxStatus.Sent : operation is 3 ? OutboxStatus.DeadLettered
            : operation is 2 ? OutboxStatus.Staged : OutboxStatus.Failed));
        if (operation != 0) { persisted["retryCount"].AsInt32.ShouldBe(2); }
    }

    [Fact]
    public async Task PreimageProjectionAndConditionalUpdateShareTheCommandServerNow()
    {
        fixture.DockerAvailable.ShouldBeTrue("computed findAndModify projection must be verified on a real server; never skipped");
        var client = new MongoClient(fixture.ConnectionString);
        var collection = client.GetDatabase(fixture.DatabaseName).GetCollection<BsonDocument>("same_now_" + Guid.NewGuid().ToString("N"));
        await collection.InsertOneAsync(new BsonDocument { { "_id", "control" }, { "value", 0 } });
        var update = new PipelineUpdateDefinition<BsonDocument>(PipelineDefinition<BsonDocument, BsonDocument>.Create(
            new BsonDocument("$set", new BsonDocument { { "value", 1 }, { "updatedAt", "$$NOW" } })));
        var before = await collection.FindOneAndUpdateAsync(new BsonDocument("_id", "control"), update,
            new FindOneAndUpdateOptions<BsonDocument, BsonDocument>
            {
                ReturnDocument = ReturnDocument.Before,
                Projection = new BsonDocument { { "_id", 0 }, { "previous", "$value" }, { "projectedAt", "$$NOW" } },
            });
        before["previous"].AsInt32.ShouldBe(0);
        var after = await collection.Find(new BsonDocument("_id", "control")).SingleAsync();
        after["value"].AsInt32.ShouldBe(1);
        after["updatedAt"].ShouldBe(before["projectedAt"], "update and preimage projection must share the same command NOW");
        after.Contains("projectedAt").ShouldBeFalse();
        after.Contains("previous").ShouldBeFalse();
    }

    private static ValueTask<OutboxCompletionOutcome> Complete(MongoDbOutboxStore store, string id, string claim, int operation, CancellationToken token)
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
