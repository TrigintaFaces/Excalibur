// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1506 // Excessive class coupling -- integration-style tests require many DI types

using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.Projections;

/// <summary>
/// Extended tests for <see cref="EphemeralProjectionEngine"/> covering:
/// - Custom JsonSerializerOptions injection (new feature)
/// - Cache hit path (return cached without replay)
/// - Skipping events where deserialization returns null
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EphemeralProjectionEngineExtendedShould
{
	private readonly InMemoryProjectionRegistry _registry = new();
	private readonly IEventStore _eventStore = A.Fake<IEventStore>();
	private readonly IEventSerializer _serializer = A.Fake<IEventSerializer>();
	private readonly NullLogger<EphemeralProjectionEngine> _logger = NullLogger<EphemeralProjectionEngine>.Instance;

	private void RegisterOrderSummaryProjection(TimeSpan? cacheTtl = null)
	{
		var msp = new MultiStreamProjection<OrderSummary>();
		msp.AddHandler<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});

		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary),
			ProjectionMode.Ephemeral,
			msp,
			inlineApply: null,
			cacheTtl: cacheTtl));
	}

	[Fact]
	public void AcceptCustomJsonSerializerOptions()
	{
		// Arrange — custom JsonSerializerOptions with specific settings
		var customOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = true,
		};

		// Act — construction should succeed with custom options
		var engine = new EphemeralProjectionEngine(
			_eventStore, _serializer, _registry, _logger,
			cache: null, jsonOptions: customOptions);

		// Assert — engine created successfully
		engine.ShouldNotBeNull();
	}

	[Fact]
	public void UseDefaultJsonOptionsWhenNoneProvided()
	{
		// Act — no jsonOptions parameter
		var engine = new EphemeralProjectionEngine(
			_eventStore, _serializer, _registry, _logger);

		// Assert — should not throw
		engine.ShouldNotBeNull();
	}

	[Fact]
	public async Task UseCacheHit_WithoutReplayingEvents()
	{
		// Arrange — register with cache TTL and set up a cache hit
		RegisterOrderSummaryProjection(cacheTtl: TimeSpan.FromMinutes(5));

		var cachedProjection = new OrderSummary { Total = 999m, EventCount = 42 };
		var cachedBytes = JsonSerializer.SerializeToUtf8Bytes(cachedProjection, new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
		});

		var cache = A.Fake<IDistributedCache>();
		A.CallTo(() => cache.GetAsync("ephemeral:OrderSummary:order-1", A<CancellationToken>._))
			.Returns(cachedBytes);

		var engine = new EphemeralProjectionEngine(
			_eventStore, _serializer, _registry, _logger, cache);

		// Act
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — returned from cache, event store never called
		result.ShouldNotBeNull();
		result.Total.ShouldBe(999m);
		result.EventCount.ShouldBe(42);

		A.CallTo(() => _eventStore.LoadAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task CustomJsonOptions_UsedForCacheSerialization()
	{
		// Arrange — use camelCase naming policy
		var customOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
		};

		var msp = new MultiStreamProjection<OrderSummary>();
		msp.AddHandler<TestOrderPlaced>((proj, e) =>
		{
			proj.Total = e.Amount;
			proj.EventCount++;
		});
		_registry.Register(new ProjectionRegistration(
			typeof(OrderSummary), ProjectionMode.Ephemeral, msp,
			inlineApply: null, cacheTtl: TimeSpan.FromMinutes(5)));

		var orderPlaced = new TestOrderPlaced { AggregateId = "order-1", Amount = 200m, Version = 1 };
		A.CallTo(() => _serializer.ResolveType(nameof(TestOrderPlaced))).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>._, typeof(TestOrderPlaced))).Returns(orderPlaced);
		A.CallTo(() => _eventStore.LoadAsync("order-1", "Order", A<CancellationToken>._))
			.Returns(new List<StoredEvent>
			{
				new("e1", "order-1", "Order", nameof(TestOrderPlaced), new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow),
			});

		byte[]? capturedBytes = null;
		var cache = A.Fake<IDistributedCache>();
		A.CallTo(() => cache.GetAsync(A<string>._, A<CancellationToken>._)).Returns((byte[]?)null);
		A.CallTo(() => cache.SetAsync(A<string>._, A<byte[]>._, A<DistributedCacheEntryOptions>._, A<CancellationToken>._))
			.Invokes((string _, byte[] bytes, DistributedCacheEntryOptions _, CancellationToken _) =>
				capturedBytes = bytes);

		var engine = new EphemeralProjectionEngine(
			_eventStore, _serializer, _registry, _logger, cache, customOptions);

		// Act
		await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None).ConfigureAwait(false);

		// Assert — bytes were cached using custom options (camelCase property names)
		capturedBytes.ShouldNotBeNull();
		var json = System.Text.Encoding.UTF8.GetString(capturedBytes);
		json.ShouldContain("\"total\""); // camelCase from custom options
	}

	[Fact]
	public async Task SkipEventsWhereDeserializationReturnsNull()
	{
		// Arrange — serializer returns null for one event
		RegisterOrderSummaryProjection();

		A.CallTo(() => _serializer.ResolveType("GoodEvent")).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => _serializer.ResolveType("NullEvent")).Returns(typeof(TestOrderPlaced));

		A.CallTo(() => _serializer.DeserializeEvent(
				A<byte[]>.That.Matches(b => b.Length > 0 && b[0] == 1), typeof(TestOrderPlaced)))
			.Returns(new TestOrderPlaced { Amount = 50m });
		A.CallTo(() => _serializer.DeserializeEvent(
				A<byte[]>.That.Matches(b => b.Length > 0 && b[0] == 2), typeof(TestOrderPlaced)))
			.Returns(null!); // deserialization returns null

		A.CallTo(() => _eventStore.LoadAsync("order-1", "Order", A<CancellationToken>._))
			.Returns(new List<StoredEvent>
			{
				new("e1", "order-1", "Order", "GoodEvent", new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow),
				new("e2", "order-1", "Order", "NullEvent", new byte[] { 2 }, null, 2, DateTimeOffset.UtcNow),
			});

		var engine = new EphemeralProjectionEngine(
			_eventStore, _serializer, _registry, _logger);

		// Act
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — only the good event was applied, null event skipped
		result.Total.ShouldBe(50m);
		result.EventCount.ShouldBe(1);
	}

	[Fact]
	public async Task NoCacheInteraction_WhenNullCache()
	{
		// Arrange — no cache passed at all (not even a fake)
		RegisterOrderSummaryProjection(cacheTtl: TimeSpan.FromMinutes(5));

		A.CallTo(() => _serializer.ResolveType(A<string>._)).Returns(typeof(TestOrderPlaced));
		A.CallTo(() => _serializer.DeserializeEvent(A<byte[]>._, A<Type>._))
			.Returns(new TestOrderPlaced { Amount = 10m });
		A.CallTo(() => _eventStore.LoadAsync("order-1", "Order", A<CancellationToken>._))
			.Returns(new List<StoredEvent>
			{
				new("e1", "order-1", "Order", nameof(TestOrderPlaced), new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow),
			});

		var engine = new EphemeralProjectionEngine(
			_eventStore, _serializer, _registry, _logger, cache: null);

		// Act — should work fine without cache
		var result = await engine.BuildAsync<OrderSummary>("order-1", "Order", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Total.ShouldBe(10m);
	}
}
