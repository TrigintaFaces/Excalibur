// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Data.DynamoDb.Caching;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.DynamoDb.Caching;

[Trait("Category", "Unit")]
[Trait("Component", "DynamoDb")]
[Trait("Pattern", "Serialization")]
public sealed class DaxCacheSerializationShould
{
	[Fact]
	public async Task RetainDetachedSnapshotsForWritesAndEveryRead()
	{
		var cache = Create(DaxCacheTestJsonContext.Default);
		var original = new DaxCacheMutableItem { Name = "original", Values = ["first", null] };
		await cache.PutItemAsync("table", "pk", null, original, CancellationToken.None);
		original.Name = "changed";
		original.Values[0] = "changed";
		var first = await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None);
		first.ShouldNotBeNull();
		first.Name.ShouldBe("original");
		first.Values.ShouldBe(new string?[] { "first", null });
		first.Name = "changed after read";
		first.Values.Clear();
		var second = await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None);
		second.ShouldNotBeNull();
		second.ShouldNotBeSameAs(first);
		second.Name.ShouldBe("original");
		second.Values.ShouldBe(new string?[] { "first", null });
	}

	[Fact]
	public async Task RefuseAnUnregisteredTypeWithoutReflectionFallback()
	{
		var cache = Create(DaxCacheTestJsonContext.Default);
		var exception = await Should.ThrowAsync<NotSupportedException>(() =>
			cache.PutItemAsync("table", "pk", null, new DaxCacheUnregisteredItem(), CancellationToken.None));
		exception.Message.ShouldContain(nameof(DaxCacheUnregisteredItem));
		exception.Message.ShouldContain("DaxCacheOptions.JsonTypeInfoResolver");
		(await cache.GetItemAsync<DaxCacheUnregisteredItem>("table", "pk", null, CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task RefuseAnUnregisteredRuntimeTypeInsideAnObjectMember()
	{
		var cache = Create(DaxCacheTestJsonContext.Default);
		await Should.ThrowAsync<NotSupportedException>(() => cache.PutItemAsync("table", "pk", null,
			new DaxCacheObjectItem { Value = new DaxCacheUnregisteredItem() }, CancellationToken.None));
		(await cache.GetItemAsync<DaxCacheObjectItem>("table", "pk", null, CancellationToken.None)).ShouldBeNull();
	}

	[Fact]
	public async Task SerializeTheDeclaredGenericTypeRatherThanTheRuntimeType()
	{
		var cache = Create(DaxCacheTestJsonContext.Default);
		await cache.PutItemAsync<DaxCacheBaseItem>("table", "pk", null,
			new DaxCacheDerivedItem { Name = "base", Extra = "derived" }, CancellationToken.None);
		var item = await cache.GetItemAsync<DaxCacheBaseItem>("table", "pk", null, CancellationToken.None);
		item.ShouldNotBeNull();
		item.GetType().ShouldBe(typeof(DaxCacheBaseItem));
		item.Name.ShouldBe("base");
	}

	[Fact]
	public async Task PreserveConsumerContextOptionsAndConverters()
	{
		var converter = new DaxCacheMutableItemConverter();
		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		options.Converters.Add(converter);
		var context = new DaxCacheTestJsonContext(options);
		var cache = Create(context);
		await cache.PutItemAsync("table", "pk", null, new DaxCacheMutableItem { Name = "converted" }, CancellationToken.None);
		var item = await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None);
		item.ShouldNotBeNull();
		item.Name.ShouldBe("converted");
		converter.Writes.ShouldBe(1);
		converter.Reads.ShouldBe(1);
		converter.CamelCaseObserved.ShouldBeTrue();
	}

	[Fact]
	public async Task AvoidResolvingMetadataForCacheMisses()
	{
		var resolver = new DaxCacheRejectingResolver();
		var cache = Create(resolver);
		(await cache.GetItemAsync<DaxCacheMutableItem>("table", "missing", null, CancellationToken.None)).ShouldBeNull();
		resolver.Calls.ShouldBe(0);
	}

	[Fact]
	public async Task SkipAnUnregisteredNewItemWhenTheCacheIsAtCapacity()
	{
		var cache = Create(DaxCacheTestJsonContext.Default);
		for (var index = 0; index < 1024; index++)
		{
			await cache.PutItemAsync("table", index.ToString(System.Globalization.CultureInfo.InvariantCulture), null,
				new DaxCacheMutableItem { Name = "cached" }, CancellationToken.None);
		}

		await cache.PutItemAsync("table", "overflow", null, new DaxCacheUnregisteredItem(), CancellationToken.None);
		(await cache.GetItemAsync<DaxCacheUnregisteredItem>("table", "overflow", null, CancellationToken.None)).ShouldBeNull();
		(await cache.GetItemAsync<DaxCacheMutableItem>("table", "0", null, CancellationToken.None)).ShouldNotBeNull();
	}

	private static InMemoryDaxCacheProvider Create(IJsonTypeInfoResolver resolver) => new(
		Options.Create(new DaxCacheOptions { JsonTypeInfoResolver = resolver }),
		NullLogger<InMemoryDaxCacheProvider>.Instance);

	[Fact]
	public async Task PreserveAConcurrentReplacementWhenRemovingAnExpiredEntry()
	{
		var clock = new DaxCacheTestTimeProvider();
		var cache = Create(clock);
		await cache.PutItemAsync("table", "pk", null, new DaxCacheMutableItem { Name = "expired" }, CancellationToken.None);
		clock.Advance(TimeSpan.FromMinutes(5));
		// Get has already captured E1 before reading the clock. Publish E2 at that exact seam.
		// The callback disarms itself before the nested Put reads this clock.
		clock.OnNextRead = () =>
		{
			var replacement = cache.PutItemAsync("table", "pk", null,
				new DaxCacheMutableItem { Name = "fresh" }, CancellationToken.None);
			replacement.IsCompletedSuccessfully.ShouldBeTrue(
				"the controlled in-memory replacement must complete before the reader resumes");
		};

		(await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None)).ShouldBeNull();
		var replacement = await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None);
		replacement.ShouldNotBeNull("expiry cleanup for E1 must not remove the concurrently published E2");
		replacement.Name.ShouldBe("fresh");
	}

	[Theory]
	[InlineData(299, true)]
	[InlineData(300, false)]
	[InlineData(301, false)]
	public async Task ExpireAtTheExactTtlBoundary(int elapsedSeconds, bool expectedHit)
	{
		var clock = new DaxCacheTestTimeProvider();
		var cache = Create(clock);
		await cache.PutItemAsync("table", "pk", null, new DaxCacheMutableItem { Name = "cached" }, CancellationToken.None);
		clock.Advance(TimeSpan.FromSeconds(elapsedSeconds));
		var item = await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None);
		if (expectedHit)
		{
			item.ShouldNotBeNull();
			item.Name.ShouldBe("cached");
		}
		else
		{
			item.ShouldBeNull();
			(await cache.GetItemAsync<DaxCacheMutableItem>("table", "pk", null, CancellationToken.None)).ShouldBeNull();
		}
	}

	private static InMemoryDaxCacheProvider Create(TimeProvider timeProvider) => new(
		Options.Create(new DaxCacheOptions { JsonTypeInfoResolver = DaxCacheTestJsonContext.Default }),
		NullLogger<InMemoryDaxCacheProvider>.Instance,
		timeProvider);

	[Fact]
	public async Task ReleaseCapacityWhenRemovingAnOrdinarilyExpiredEntry()
	{
		var clock = new DaxCacheTestTimeProvider();
		var cache = Create(clock);
		for (var index = 0; index < 1024; index++)
		{
			await cache.PutItemAsync("table", index.ToString(System.Globalization.CultureInfo.InvariantCulture), null,
				new DaxCacheMutableItem { Name = "old" }, CancellationToken.None);
		}

		clock.Advance(TimeSpan.FromMinutes(5));
		(await cache.GetItemAsync<DaxCacheMutableItem>("table", "0", null, CancellationToken.None)).ShouldBeNull();
		await cache.PutItemAsync("table", "overflow", null, new DaxCacheMutableItem { Name = "fresh" }, CancellationToken.None);
		var admitted = await cache.GetItemAsync<DaxCacheMutableItem>("table", "overflow", null, CancellationToken.None);
		admitted.ShouldNotBeNull("ordinary expiry must release capacity for a new item");
		admitted.Name.ShouldBe("fresh");
	}
}

internal sealed class DaxCacheTestTimeProvider : TimeProvider
{
	private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	internal Action? OnNextRead { get; set; }
	internal void Advance(TimeSpan duration) => _utcNow += duration;
	public override DateTimeOffset GetUtcNow()
	{
		var callback = OnNextRead;
		OnNextRead = null;
		callback?.Invoke();
		return _utcNow;
	}
}

internal sealed class DaxCacheMutableItem
{
	public string? Name { get; set; }
	public List<string?> Values { get; set; } = [];
}

internal class DaxCacheBaseItem
{
	public string? Name { get; set; }
}

internal sealed class DaxCacheDerivedItem : DaxCacheBaseItem
{
	public string? Extra { get; set; }
}

internal sealed class DaxCacheObjectItem
{
	public object? Value { get; set; }
}

internal sealed class DaxCacheUnregisteredItem;

[JsonSerializable(typeof(DaxCacheMutableItem))]
[JsonSerializable(typeof(DaxCacheBaseItem))]
[JsonSerializable(typeof(DaxCacheObjectItem))]
internal sealed partial class DaxCacheTestJsonContext : JsonSerializerContext;

internal sealed class DaxCacheRejectingResolver : IJsonTypeInfoResolver
{
	public int Calls { get; private set; }
	public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
	{
		Calls++;
		throw new InvalidOperationException("A cache miss must not resolve metadata.");
	}
}

internal sealed class DaxCacheMutableItemConverter : JsonConverter<DaxCacheMutableItem>
{
	public int Writes { get; private set; }
	public int Reads { get; private set; }
	public bool CamelCaseObserved { get; private set; }
	public override DaxCacheMutableItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		Reads++;
		CamelCaseObserved |= options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase;
		return new DaxCacheMutableItem { Name = reader.GetString() };
	}
	public override void Write(Utf8JsonWriter writer, DaxCacheMutableItem value, JsonSerializerOptions options)
	{
		Writes++;
		CamelCaseObserved |= options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase;
		writer.WriteStringValue(value.Name);
	}
}
