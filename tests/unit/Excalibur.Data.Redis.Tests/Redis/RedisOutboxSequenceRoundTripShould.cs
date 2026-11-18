// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
public sealed class RedisOutboxSequenceRoundTripShould
{
    private static readonly byte[] Payload = [1];

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public async Task PassExactInvariantInt64AsAnOpaqueLuaHashArgument(long sequence)
    {
        var connection = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        var writes = 0;
        string? observed = null;
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._, A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                writes++;
                for (var index = 2; index < values.Length; index += 2)
                {
                    if (string.Equals(values[index].ToString(), "SequenceNumber", StringComparison.Ordinal))
                    {
                        observed = values[index + 1].ToString();
                    }
                }
                return Task.FromResult(RedisResult.Create((RedisValue)"SUCCESS"));
            });
        await using var store = Store(connection);
        await store.StageMessageAsync(new OutboundMessage("event", Payload, "destination")
        {
            Id = "sequence", PartitionKey = "partition", SequenceNumber = sequence,
        }, CancellationToken.None).ConfigureAwait(false);
        writes.ShouldBe(1);
        observed.ShouldBe(sequence.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(7L)]
    [InlineData(9007199254740993L)]
    [InlineData(long.MaxValue)]
    public async Task ReadExactInt64FromTheClaimedHashWithoutFloatingPointConversion(long sequence)
    {
        var entries = BaseEntries();
        entries.Add(new HashEntry("SequenceNumber", sequence.ToString(CultureInfo.InvariantCulture)));
        await using var store = Store(Backend(entries));
        var receipt = (await store.GetUnsentMessagesAsync(1, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
        receipt.SequenceNumber.ShouldBe(sequence);
        receipt.PartitionKey.ShouldBe("partition");
    }

    [Fact]
    public async Task ReadMissingSequenceAsUnsequencedZero()
    {
        await using var store = Store(Backend(BaseEntries()));
        var receipt = (await store.GetUnsentMessagesAsync(1, CancellationToken.None).ConfigureAwait(false)).ShouldHaveSingleItem();
        receipt.SequenceNumber.ShouldBe(0L);
    }

    private static RedisOutboxStore Store(IConnectionMultiplexer connection) => new(connection,
        Options.Create(new RedisOutboxOptions { ConnectionString = "localhost:6379", KeyPrefix = "outbox" }),
        NullLogger<RedisOutboxStore>.Instance);

    private static IConnectionMultiplexer Backend(List<HashEntry> entries)
    {
        var connection = A.Fake<IConnectionMultiplexer>();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
                Task.FromResult(RedisOutboxClaimReply.Array(RedisOutboxClaimReply.Snapshot("sequence", entries, values[2].ToString()))));
        return connection;
    }

    private static List<HashEntry> BaseEntries() =>
    [
        new("MessageType", "event"), new("Payload", Payload), new("Destination", "destination"),
        new("CreatedAt", 123456789L), new("Status", (int)OutboxStatus.Staged), new("PartitionKey", "partition"),
    ];
}
