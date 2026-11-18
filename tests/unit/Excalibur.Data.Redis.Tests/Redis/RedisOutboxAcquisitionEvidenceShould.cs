// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Outbox")]
public sealed class RedisOutboxAcquisitionEvidenceShould
{

    [Fact]
    public async Task CaptureMintedIdentityInsteadOfReadingASuccessorsHashOwner()
    {
        var connection = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        var identities = new List<string>();
        HashEntry[] entries = [new("MessageType", "event"), new("Payload", "body"), new("Destination", "sink"),
            new("CreatedAt", 0), new("Status", 0), new("LeasedBy", "successor-that-won-after-script")];
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                var minted = values[2].ToString();
                identities.Add(minted);
                // The claim script stamps this owner before capturing the reply hash.
                return Task.FromResult(RedisOutboxClaimReply.Array(RedisOutboxClaimReply.Snapshot("row", entries, minted)));
            });
        await using var store = new RedisOutboxStore(connection, Options.Create(new RedisOutboxOptions
        {
            ConnectionString = "localhost:6379", ProcessorId = "same-process", KeyPrefix = "outbox",
        }), NullLogger<RedisOutboxStore>.Instance);
        var first = (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();
        var second = (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();
        identities.Count.ShouldBe(2);
        identities[0].ShouldNotBe(identities[1]);
        first.DispatcherId.ShouldBe(identities[0]);
        second.DispatcherId.ShouldBe(identities[1]);
        first.DispatcherId.ShouldNotBe("successor-that-won-after-script");
        A.CallTo(() => connection.Dispose()).MustNotHaveHappened();
    }
}
