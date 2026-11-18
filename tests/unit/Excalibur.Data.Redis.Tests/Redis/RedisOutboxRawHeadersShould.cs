// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;

using StackExchange.Redis;

namespace Excalibur.Data.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Pattern", "Serialization")]
public sealed class RedisOutboxRawHeadersShould
{
	private static readonly byte[] ExpectedPayload = [0, 255, 1, 127];
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("null")]
	[InlineData("{}")]
	[InlineData("{\"Header\":\"upper\",\"header\":\"lower\",\"same\":1,\"same\":2}")]
	[InlineData("{\"text\":\"value\",\"number\":1.25,\"flag\":true,\"null\":null,\"array\":[1,\"two\"],\"object\":{\"Name\":\"nested\"}}")]
	public async Task PreserveClaimedOpaqueBytesRoutingAndClosedHeaderValues(string? json)
	{
		var (connection, database) = CreateBackend(json);
		await using var store = CreateStore(connection);
		IOutboxStore contract = store;
		var row = (await contract.GetUnsentMessagesAsync(1, CancellationToken.None)).ShouldHaveSingleItem();
		row.Id.ShouldBe("raw-1");
		row.MessageType.ShouldBe("unregistered.consumer.contract");
		row.Payload.ShouldBe(ExpectedPayload, "raw queries must not decode or replace a consumer payload");
		row.Destination.ShouldBe("destination");
		row.TenantId.ShouldBe("tenant-A");
		row.PartitionKey.ShouldBe("partition-A");
		row.GroupKey.ShouldBe("group-A");
		row.CorrelationId.ShouldBe("correlation-A");
		row.CausationId.ShouldBe("causation-A");
		row.TargetTransports.ShouldBe("transport-A,transport-B");
		var expected = string.IsNullOrEmpty(json)
			? new Dictionary<string, object>(StringComparer.Ordinal)
			: JsonSerializer.Deserialize<Dictionary<string, object>>(json, EventSerializationDefaults.Canonical)
			  ?? new Dictionary<string, object>(StringComparer.Ordinal);
		row.Headers.Keys.Order(StringComparer.Ordinal).ShouldBe(expected.Keys.Order(StringComparer.Ordinal));
		foreach (var header in expected)
		{
			if (header.Value is null) { row.Headers[header.Key].ShouldBeNull(); continue; }
			var actual = row.Headers[header.Key].ShouldBeOfType<JsonElement>();
			actual.GetRawText().ShouldBe(header.Value.ShouldBeOfType<JsonElement>().GetRawText());
		}
		A.CallTo(() => database.HashGetAllAsync("outbox:msg:raw-1", A<CommandFlags>._)).MustNotHaveHappened();
		A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
			A<RedisValue[]>._, A<CommandFlags>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => connection.Dispose()).MustNotHaveHappened();
	}

	[Theory]
	[InlineData(" ")]
	[InlineData("{")]
	[InlineData("[]")]
	[InlineData("{\"value\":}")]
	public async Task ReportMalformedPersistedHeadersInsteadOfSilentlyDroppingAClaimedRow(string json)
	{
		var (connection, database) = CreateBackend(json);
		await using var store = CreateStore(connection);
		await Should.ThrowAsync<JsonException>(() => store.GetUnsentMessagesAsync(1, CancellationToken.None).AsTask());
		A.CallTo(() => database.HashGetAllAsync("outbox:msg:raw-1", A<CommandFlags>._)).MustNotHaveHappened();
		A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
			A<RedisValue[]>._, A<CommandFlags>._)).MustHaveHappenedOnceExactly();
	}

	private static RedisOutboxStore CreateStore(IConnectionMultiplexer connection) => new(connection,
		Options.Create(new RedisOutboxOptions { ConnectionString = "localhost:6379", KeyPrefix = "outbox" }),
		NullLogger<RedisOutboxStore>.Instance);

	private static (IConnectionMultiplexer Connection, IDatabase Database) CreateBackend(string? json)
	{
		var connection = A.Fake<IConnectionMultiplexer>();
		var database = A.Fake<IDatabase>();
		A.CallTo(() => connection.GetDatabase(A<int>._, A<object>._)).Returns(database);
		var entries = new List<HashEntry>
		{
			new("MessageType", "unregistered.consumer.contract"), new("Payload", ExpectedPayload),
			new("Destination", "destination"), new("CreatedAt", 123456789L), new("Status", (int)OutboxStatus.Staged),
			new("Priority", 3), new("RetryCount", 2), new("TenantId", "tenant-A"), new("PartitionKey", "partition-A"),
			new("GroupKey", "group-A"), new("CorrelationId", "correlation-A"), new("CausationId", "causation-A"),
			new("TargetTransports", "transport-A,transport-B"),
		};
		if (json is not null) { entries.Add(new HashEntry("Headers", json)); }
		A.CallTo(() => database.ScriptEvaluateAsync(A<string>._, A<RedisKey[]>._,
				A<RedisValue[]>._, A<CommandFlags>._))
			.ReturnsLazily((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
				Task.FromResult(RedisOutboxClaimReply.Array(RedisOutboxClaimReply.Snapshot("raw-1", entries, values[2].ToString()))));
		return (connection, database);
	}
}
