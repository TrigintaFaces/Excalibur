// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Tests.Redis;

[Trait("Category", "Unit")]
public sealed class RedisEventStoreShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var connection = CreateUninitializedConnection();
		var options = Options.Create(new RedisEventStoreOptions { ConnectionString = "localhost:6379" });
		var logger = NullLogger<RedisEventStore>.Instance;

		Should.Throw<ArgumentNullException>(() => new RedisEventStore(null!, options, logger))
			.ParamName.ShouldBe("connection");
		Should.Throw<ArgumentNullException>(() => new RedisEventStore(connection, null!, logger))
			.ParamName.ShouldBe("options");
		Should.Throw<ArgumentNullException>(() => new RedisEventStore(connection, options, null!))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public async Task ValidateMethodGuardsBeforeRedisAccess()
	{
		var sut = (RedisEventStore)RuntimeHelpers.GetUninitializedObject(typeof(RedisEventStore));

		await Should.ThrowAsync<ArgumentException>(() => sut.LoadAsync("", "Order", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.LoadAsync("agg-1", " ", CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.LoadAsync("", "Order", 0, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.LoadAsync("agg-1", " ", 0, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.AppendAsync("", "Order", [], 0, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.AppendAsync("agg-1", " ", [], 0, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentNullException>(() => sut.AppendAsync("agg-1", "Order", null!, 0, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => sut.GetUndispatchedEventsAsync(0, CancellationToken.None).AsTask());
		await Should.ThrowAsync<ArgumentException>(() => sut.MarkEventAsDispatchedAsync(" ", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = (RedisEventStore)RuntimeHelpers.GetUninitializedObject(typeof(RedisEventStore));

		var result = await sut.AppendAsync("agg-1", "Order", [], expectedVersion: 7, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(7);
		result.FirstEventPosition.ShouldBe(0);
	}

	[Fact]
	public void ParseStreamEntries_ReturnStoredEvents()
	{
		var stored = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "Order",
			EventType: "OrderPlaced",
			EventData: JsonSerializer.SerializeToUtf8Bytes(new { Id = 42 }),
			Metadata: null,
			Version: 5,
			Timestamp: DateTimeOffset.UtcNow,
			IsDispatched: false);

		var json = JsonSerializer.Serialize(stored);
		var entries = new[]
		{
			new StreamEntry("1-0", [new NameValueEntry("evt-1", json)])
		};

		var method = typeof(RedisEventStore).GetMethod("ParseStreamEntries", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var parsed = (List<StoredEvent>)method!.Invoke(null, [entries])!;
		parsed.Count.ShouldBe(1);
		parsed[0].EventId.ShouldBe("evt-1");
		parsed[0].AggregateId.ShouldBe("agg-1");
		parsed[0].AggregateType.ShouldBe("Order");
		parsed[0].Version.ShouldBe(5);
	}

	[Fact]
	public void ParseStreamEntries_SkipNullPayloadAndOnlyReadFirstField()
	{
		var entries = new[]
		{
			new StreamEntry("1-0", [new NameValueEntry("evt-1", "null"), new NameValueEntry("evt-ignored", "{}")])
		};

		var method = typeof(RedisEventStore).GetMethod("ParseStreamEntries", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var parsed = (List<StoredEvent>)method!.Invoke(null, [entries])!;
		parsed.ShouldBeEmpty();
	}

	private static ConnectionMultiplexer CreateUninitializedConnection() =>
		(ConnectionMultiplexer)RuntimeHelpers.GetUninitializedObject(typeof(ConnectionMultiplexer));
}
