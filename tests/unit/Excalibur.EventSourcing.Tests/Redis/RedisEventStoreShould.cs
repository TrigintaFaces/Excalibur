// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.EventSourcing.Tests.Redis;

[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class RedisEventStoreShould : UnitTestBase
{
	[Fact]
	public void ValidateConstructorGuards()
	{
		var connection = CreateUninitializedConnection();
		var options = Options.Create(new RedisEventStoreOptions { ConnectionString = "localhost:6379" });
		var logger = NullLogger<RedisEventStore>.Instance;
		var tenantContext = new SingleTenantDefaultContext();

		Should.Throw<ArgumentNullException>(() => new RedisEventStore(null!, options, logger, tenantContext))
			.ParamName.ShouldBe("connection");
		Should.Throw<ArgumentNullException>(() => new RedisEventStore(connection, null!, logger, tenantContext))
			.ParamName.ShouldBe("options");
		Should.Throw<ArgumentNullException>(() => new RedisEventStore(connection, options, null!, tenantContext))
			.ParamName.ShouldBe("logger");
		Should.Throw<ArgumentNullException>(() => new RedisEventStore(connection, options, logger, null!))
			.ParamName.ShouldBe("tenantContext");
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
	}

	[Fact]
	public async Task AppendAsync_ReturnSuccess_WhenNoEvents()
	{
		var sut = (RedisEventStore)RuntimeHelpers.GetUninitializedObject(typeof(RedisEventStore));

		var result = await sut.AppendAsync("agg-1", "Order", [], expectedVersion: 7, CancellationToken.None);

		result.Success.ShouldBeTrue();
		result.NextExpectedVersion.ShouldBe(7);
		result.FirstEventPosition.ShouldBeNull();
	}

	[Fact]
	public void ParseStreamEntries_ReturnStoredEvents()
	{
		// The store serializes stream entries with the canonical options (camelCase), so the read path must
		// deserialize with the SAME options — a default-serialized (PascalCase) envelope would fail to bind.
		// Serialize the fixture with the canonical factory to mirror the production write contract exactly.
		var canonicalOptions = EventSerializationDefaults.CreateCanonicalOptions();

		var stored = new StoredEvent(
			EventId: "evt-1",
			AggregateId: "agg-1",
			AggregateType: "Order",
			EventType: "OrderPlaced",
			EventData: JsonSerializer.SerializeToUtf8Bytes(new { Id = 42 }, canonicalOptions),
			Metadata: null,
			Version: 5,
			Timestamp: DateTimeOffset.UtcNow);

		var json = JsonSerializer.Serialize(stored, canonicalOptions);
		var entries = new[]
		{
			new StreamEntry("1-0", [new NameValueEntry("evt-1", json)])
		};

		var method = typeof(RedisEventStore).GetMethod("ParseStreamEntries", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var parsed = (List<StoredEvent>)method!.Invoke(null, [entries, canonicalOptions])!;
		parsed.Count.ShouldBe(1);
		parsed[0].EventId.ShouldBe("evt-1");
		parsed[0].AggregateId.ShouldBe("agg-1");
		parsed[0].AggregateType.ShouldBe("Order");
		parsed[0].Version.ShouldBe(5);
	}

	[Fact]
	public void ParseStreamEntries_SkipNullPayloadAndOnlyReadFirstField()
	{
		var canonicalOptions = EventSerializationDefaults.CreateCanonicalOptions();

		var entries = new[]
		{
			new StreamEntry("1-0", [new NameValueEntry("evt-1", "null"), new NameValueEntry("evt-ignored", "{}")])
		};

		var method = typeof(RedisEventStore).GetMethod("ParseStreamEntries", BindingFlags.NonPublic | BindingFlags.Static);
		method.ShouldNotBeNull();

		var parsed = (List<StoredEvent>)method!.Invoke(null, [entries, canonicalOptions])!;
		parsed.ShouldBeEmpty();
	}

	private static ConnectionMultiplexer CreateUninitializedConnection() =>
		(ConnectionMultiplexer)RuntimeHelpers.GetUninitializedObject(typeof(ConnectionMultiplexer));

	/// <summary>Mirrors the framework single-tenant default: always present, always the one canonical tenant.</summary>
	private sealed class SingleTenantDefaultContext : ITenantContext
	{
		public string? TenantId => TenantDefaults.DefaultTenantId;

		public bool HasTenant => true;
	}
}
