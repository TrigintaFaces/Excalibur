// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;
using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Excalibur.Data.Tests.Redis;

internal static class RedisOutboxClaimReply
{
    internal static RedisResult String(RedisValue value) => RedisResult.Create(value, ResultType.BulkString);
    internal static RedisResult Array(params RedisResult[] items) => RedisResult.Create(items);

    // Represents the actual server HSET followed by HGETALL. It is not a production owner-repair rule.
    internal static RedisResult Snapshot(string id, IEnumerable<HashEntry> fields, string stampedOwner)
    {
        var pairs = fields.Where(field => field.Name != "LeasedBy")
            .Append(new HashEntry("LeasedBy", stampedOwner))
            .SelectMany(field => new[] { String(field.Name), String(field.Value) }).ToArray();
        return Array(String(id), Array(pairs));
    }
}

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Outbox")]
public sealed class RedisOutboxClaimSnapshotResponseShould
{
    public static IEnumerable<TheoryDataRow<string, bool>> MalformedCases()
    {
        string[] cases = ["OuterInteger", "TooManyRows", "NullRow", "OneElementRow", "ThreeElementRow",
            "EmptyId", "NullId", "IntegerId", "DuplicateId", "NullHash", "EmptyHash", "OddHash",
            "NullName", "IntegerName", "InvalidTextName", "DuplicateField", "MissingOwner", "DuplicateOwner",
            "NullOwner", "IntegerOwner", "MismatchedOwner", "NullValue", "ArrayValue", "MalformedHeaders"];
        foreach (var scenario in cases)
        {
            yield return new(scenario, false);
            yield return new(scenario, true);
        }
    }

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public async Task RejectWholeMalformedResponseEvenWhenCanceled(string scenario, bool cancelAfterServer)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var database = A.Fake<IDatabase>();
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                calls++;
                var response = Malformed(scenario, values[2].ToString());
                if (cancelAfterServer) { cancellation.Cancel(); }
                return Task.FromResult(response);
            });
        await using var store = Store(database);
        var batchSize = scenario == "TooManyRows" ? 1 : 2;
        var failure = await Record.ExceptionAsync(() => store.GetUnsentMessagesAsync(batchSize, cancellation.Token).AsTask());
        failure.ShouldNotBeNull();
        if (scenario == "InvalidTextName") { failure.ShouldBeOfType<DecoderFallbackException>(); }
        else if (scenario == "MalformedHeaders") { failure.ShouldBeAssignableTo<JsonException>(); }
        else { failure.ShouldBeOfType<InvalidOperationException>(); }
        calls.ShouldBe(1, "No compensating release or replacement claim may follow a malformed response.");
        A.CallTo(() => database.HashGetAllAsync(A<RedisKey>._, A<CommandFlags>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ReturnEmptyAfterPostServerCancellationOfValidResponse()
    {
        using var cancellation = new CancellationTokenSource();
        var database = A.Fake<IDatabase>();
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                var owner = values[2].ToString();
                var result = RedisOutboxClaimReply.Array(Row("first", owner), Row("second", owner));
                cancellation.Cancel();
                return Task.FromResult(result);
            });
        await using var store = Store(database);
        (await store.GetUnsentMessagesAsync(2, cancellation.Token)).ShouldBeEmpty();
        A.CallTo(() => database.HashGetAllAsync(A<RedisKey>._, A<CommandFlags>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task PreserveBinaryBytesAllMapperFieldsAndSelectionOrder()
    {
        byte[] payload = [0, 255, 128, 1, 34, 92];
        HashEntry[] fields = [new("MessageType", "event"), new("Payload", payload), new("Destination", "sink"),
            new("CreatedAt", "123456789"), new("Status", ((int)OutboxStatus.Failed).ToString()),
            new("Priority", "3"), new("RetryCount", "2"), new("LastError", "captured-error"),
            new("TenantId", "tenant"), new("PartitionKey", "partition"), new("SequenceNumber", "9223372036854775807"),
            new("GroupKey", "group"), new("CorrelationId", "correlation"), new("CausationId", "causation"),
            new("TargetTransports", "one,two"), new("IsMultiTransport", "1"), new("Headers", "{\"closed\":true}"),
            new("ScheduledAt", "100"), new("SentAt", "200"), new("LastAttemptAt", "300"), new("FutureField", "opaque")];
        string? capturedOwner = null;
        var database = A.Fake<IDatabase>();
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                capturedOwner = values[2].ToString();
                return Task.FromResult(RedisOutboxClaimReply.Array(
                    RedisOutboxClaimReply.Snapshot("first", fields, capturedOwner), Row("second", capturedOwner)));
            });
        await using var store = Store(database);
        var rows = (await store.GetUnsentMessagesAsync(2, CancellationToken.None)).ToArray();
        rows.Select(row => row.Id).ShouldBe(["first", "second"]);
        var first = rows[0];
        first.DispatcherId.ShouldBe(capturedOwner);
        first.MessageType.ShouldBe("event"); first.Payload.ShouldBe(payload); first.Destination.ShouldBe("sink");
        first.CreatedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(123456789));
        first.Status.ShouldBe(OutboxStatus.Failed); first.Priority.ShouldBe(3); first.RetryCount.ShouldBe(2);
        first.LastError.ShouldBe("captured-error"); first.TenantId.ShouldBe("tenant");
        first.PartitionKey.ShouldBe("partition"); first.SequenceNumber.ShouldBe(long.MaxValue); first.GroupKey.ShouldBe("group");
        first.CorrelationId.ShouldBe("correlation"); first.CausationId.ShouldBe("causation");
        first.TargetTransports.ShouldBe("one,two"); first.IsMultiTransport.ShouldBeTrue();
        first.Headers["closed"].ShouldBeOfType<JsonElement>().GetBoolean().ShouldBeTrue();
        first.ScheduledAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(100));
        first.SentAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(200));
        first.LastAttemptAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(300));
        A.CallTo(() => database.HashGetAllAsync(A<RedisKey>._, A<CommandFlags>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task KeepExistingLegacyDefaultsWhenOnlyCapturedOwnerIsPresent()
    {
        var database = A.Fake<IDatabase>();
        string? owner = null;
        A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
                A<RedisValue[]>._, A<CommandFlags>._))
            .ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
            {
                owner = values[2].ToString();
                return Task.FromResult(RedisOutboxClaimReply.Array(RedisOutboxClaimReply.Snapshot("legacy", [], owner)));
            });
        await using var store = Store(database);
        var row = (await store.GetUnsentMessagesAsync(1, CancellationToken.None)).Single();
        row.Id.ShouldBe("legacy"); row.DispatcherId.ShouldBe(owner); row.MessageType.ShouldBeEmpty();
        row.Payload.ShouldBeEmpty(); row.Destination.ShouldBeEmpty(); row.CreatedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        row.Status.ShouldBe(OutboxStatus.Staged); row.Priority.ShouldBe(0); row.RetryCount.ShouldBe(0);
        row.TenantId.ShouldBe(TenantScope.UntenantedSentinel); row.LastError.ShouldBeNull(); row.Headers.ShouldBeEmpty();
        row.PartitionKey.ShouldBeNull(); row.SequenceNumber.ShouldBe(0); row.GroupKey.ShouldBeNull();
        row.CorrelationId.ShouldBeNull(); row.CausationId.ShouldBeNull(); row.TargetTransports.ShouldBeNull();
        row.IsMultiTransport.ShouldBeFalse(); row.ScheduledAt.ShouldBeNull(); row.SentAt.ShouldBeNull(); row.LastAttemptAt.ShouldBeNull();
    }

    private static RedisOutboxStore Store(IDatabase database)
    {
        var connection = A.Fake<IConnectionMultiplexer>();
        A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
        return new(connection, Options.Create(new RedisOutboxOptions { ConnectionString = "localhost:6379", KeyPrefix = "outbox" }),
            NullLogger<RedisOutboxStore>.Instance);
    }

    private static RedisResult Row(string id, string owner) => RedisOutboxClaimReply.Snapshot(id, [], owner);

    private static RedisResult Malformed(string scenario, string owner)
    {
        var text = RedisOutboxClaimReply.String;
        var first = Row("first", owner);
        var goodOwner = text(owner);
        var nullValue = RedisResult.Create(RedisValue.Null, ResultType.Null);
        var integer = RedisResult.Create((RedisValue)42, ResultType.Integer);
        var empty = RedisOutboxClaimReply.Array();
        var pairs = new List<RedisResult> { text("LeasedBy"), goodOwner };
        RedisResult id = text("second");
        RedisResult? second = null;
        switch (scenario)
        {
            case "OuterInteger": return integer;
            case "TooManyRows": return RedisOutboxClaimReply.Array(first, Row("second", owner));
            case "NullRow": second = nullValue; break;
            case "OneElementRow": second = RedisOutboxClaimReply.Array(id); break;
            case "ThreeElementRow": second = RedisOutboxClaimReply.Array(id, empty, empty); break;
            case "EmptyId": id = text(""); break;
            case "NullId": id = nullValue; break;
            case "IntegerId": id = integer; break;
            case "DuplicateId": id = text("first"); break;
            case "NullHash": second = RedisOutboxClaimReply.Array(id, nullValue); break;
            case "EmptyHash": pairs.Clear(); break;
            case "OddHash": pairs.Add(text("unpaired")); break;
            case "NullName": pairs.AddRange([nullValue, text("value")]); break;
            case "IntegerName": pairs.AddRange([integer, text("value")]); break;
            case "InvalidTextName": pairs.AddRange([text((RedisValue)new byte[] { 255 }), text("value")]); break;
            case "DuplicateField": pairs.AddRange([text("unknown"), text("one"), text("unknown"), text("two")]); break;
            case "MissingOwner": pairs.Clear(); pairs.AddRange([text("unknown"), text("value")]); break;
            case "DuplicateOwner": pairs.AddRange([text("LeasedBy"), goodOwner]); break;
            case "NullOwner": pairs[1] = nullValue; break;
            case "IntegerOwner": pairs[1] = integer; break;
            case "MismatchedOwner": pairs[1] = text("successor-owner"); break;
            case "NullValue": pairs.AddRange([text("unknown"), nullValue]); break;
            case "ArrayValue": pairs.AddRange([text("unknown"), empty]); break;
            case "MalformedHeaders": pairs.AddRange([text("Headers"), text("{")]); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        second ??= RedisOutboxClaimReply.Array(id, RedisOutboxClaimReply.Array(pairs.ToArray()));
        return RedisOutboxClaimReply.Array(first, second);
    }
}
