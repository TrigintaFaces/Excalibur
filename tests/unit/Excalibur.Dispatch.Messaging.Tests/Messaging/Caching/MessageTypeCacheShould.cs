// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Comprehensive tests for <see cref="MessageTypeCache"/> covering initialization, lookup,
/// resolution, thread safety, and edge cases.
/// </summary>
[Collection("MessageTypeCacheTests")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Caching)]
public sealed class MessageTypeCacheShould : IDisposable
{
	public MessageTypeCacheShould()
	{
		ResetCache();
	}

	public void Dispose()
	{
		ResetCache();
	}

	// ---- Initialize ----

	[Fact]
	public void Initialize_WithValidTypes_PopulatesCache()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent), typeof(SampleCommand)]);

		MessageTypeCache.IsCached(typeof(SampleEvent)).ShouldBeTrue();
		MessageTypeCache.IsCached(typeof(SampleCommand)).ShouldBeTrue();
		MessageTypeCache.GetCachedTypes().Count.ShouldBe(2);
	}

	[Fact]
	public void Initialize_ThrowsOnNullEnumerable()
	{
		Should.Throw<ArgumentNullException>(() => MessageTypeCache.Initialize(null!));
	}

	[Fact]
	public void Initialize_OnlyOnce_SubsequentCallsAreNoOp()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);
		MessageTypeCache.Initialize([typeof(SampleCommand)]);

		MessageTypeCache.IsCached(typeof(SampleEvent)).ShouldBeTrue();
		MessageTypeCache.IsCached(typeof(SampleCommand)).ShouldBeFalse(
			"Second Initialize should be no-op once _initialized is true");
	}

	[Fact]
	public void Initialize_WithDuplicateTypes_DeduplicatesNaturally()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent), typeof(SampleEvent)]);

		MessageTypeCache.IsCached(typeof(SampleEvent)).ShouldBeTrue();
		MessageTypeCache.GetCachedTypes().Count.ShouldBe(1);
	}

	[Fact]
	public void Initialize_WithEmptyCollection_ProducesEmptyCache()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		MessageTypeCache.GetCachedTypes().ShouldBeEmpty();
	}

	[Fact]
	public void Initialize_ClearsFallbackCache()
	{
		// Pre-populate fallback
		var fallback = MessageTypeCache.GetOrCreateMetadata(typeof(FallbackType));
		fallback.ShouldNotBeNull();

		// Now initialize with different types
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		// The fallback cache should have been cleared
		// GetOrCreateMetadata for FallbackType should now create a new entry in fallback
		var afterInit = MessageTypeCache.GetOrCreateMetadata(typeof(FallbackType));
		afterInit.ShouldNotBeNull();
		afterInit.Type.ShouldBe(typeof(FallbackType));
	}

	// ---- GetMetadata ----

	[Fact]
	public void GetMetadata_ForCachedType_ReturnsMetadata()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var metadata = MessageTypeCache.GetMetadata(typeof(SampleEvent));

		metadata.ShouldNotBeNull();
		metadata.Type.ShouldBe(typeof(SampleEvent));
		metadata.FullName.ShouldBe(typeof(SampleEvent).FullName);
	}

	[Fact]
	public void GetMetadata_ForUncachedType_ReturnsNull()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var metadata = MessageTypeCache.GetMetadata(typeof(SampleCommand));

		metadata.ShouldBeNull();
	}

	[Fact]
	public void GetMetadata_WhenCacheEmpty_ReturnsNull()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		MessageTypeCache.GetMetadata(typeof(SampleEvent)).ShouldBeNull();
	}

	// ---- GetOrCreateMetadata ----

	[Fact]
	public void GetOrCreateMetadata_ForCachedType_ReturnsFrozenEntry()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var metadata = MessageTypeCache.GetOrCreateMetadata(typeof(SampleEvent));

		metadata.ShouldNotBeNull();
		metadata.Type.ShouldBe(typeof(SampleEvent));
	}

	[Fact]
	public void GetOrCreateMetadata_ForUncachedType_CreatesFallbackEntry()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var metadata = MessageTypeCache.GetOrCreateMetadata(typeof(SampleCommand));

		metadata.ShouldNotBeNull();
		metadata.Type.ShouldBe(typeof(SampleCommand));
	}

	[Fact]
	public void GetOrCreateMetadata_ForFallbackType_ReturnsSameInstance()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		var first = MessageTypeCache.GetOrCreateMetadata(typeof(SampleEvent));
		var second = MessageTypeCache.GetOrCreateMetadata(typeof(SampleEvent));

		ReferenceEquals(first, second).ShouldBeTrue(
			"Fallback cache should return same instance for repeated lookups");
	}

	[Fact]
	public void GetOrCreateMetadata_ConcurrentAccess_IsThreadSafe()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		var results = new MessageTypeMetadata[100];
		Parallel.For(0, 100, i =>
		{
			results[i] = MessageTypeCache.GetOrCreateMetadata(typeof(SampleEvent));
		});

		// All results should reference the same metadata
		var distinct = results.Distinct().ToList();
		distinct.Count.ShouldBe(1, "All concurrent lookups should return the same cached instance");
	}

	// ---- ResolveType ----

	[Fact]
	public void ResolveType_ByDeclaredName_ReturnsType()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var resolved = MessageTypeCache.ResolveType("cache.event");

		resolved.ShouldBe(typeof(SampleEvent));
	}

	[Fact]
	public void ResolveType_ByDeclaredAlias_ReturnsType()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var resolved = MessageTypeCache.ResolveType("cache.legacy-event");

		resolved.ShouldBe(typeof(SampleEvent));
	}

	[Fact]
	public void ResolveType_ForUnknownName_ReturnsNull()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		MessageTypeCache.ResolveType("NonExistentType").ShouldBeNull();
	}

	[Fact]
	public void ResolveType_ForNull_ReturnsNull()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		MessageTypeCache.ResolveType(null!).ShouldBeNull();
	}

	[Fact]
	public void ResolveType_ForEmptyString_ReturnsNull()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		MessageTypeCache.ResolveType(string.Empty).ShouldBeNull();
	}

	[Fact]
	public void NeverResolveUndeclaredClrNames()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent), typeof(SampleCommand)]);
		MessageTypeCache.ResolveType(typeof(SampleEvent).FullName!).ShouldBeNull();
		MessageTypeCache.ResolveType(nameof(SampleEvent)).ShouldBeNull();
	}

	[Fact]
	public void ReturnNullForUnnamedLocalMessages() => MessageTypeCache.GetTypeName(typeof(FallbackType)).ShouldBeNull();

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void RejectEveryDeclaredIdentityCollisionBeforePublishingAndAllowRetry(bool reverse)
	{
		foreach (var pair in new[]
		{
			new[] { typeof(CanonicalA), typeof(CanonicalB) },
			new[] { typeof(CanonicalA), typeof(AliasA) },
			new[] { typeof(AliasA), typeof(AliasB) },
		})
		{
			MessageTypeCache.Reset();
			Should.Throw<InvalidOperationException>(() => MessageTypeCache.Initialize(reverse ? pair.Reverse() : pair));
			MessageTypeCache.GetCachedTypes().ShouldBeEmpty();
			MessageTypeCache.ResolveType("collision").ShouldBeNull();
			MessageTypeCache.Initialize([typeof(SampleEvent), typeof(SampleEvent)]);
			MessageTypeCache.ResolveType(MessageTypeCache.GetTypeName(typeof(SampleEvent))!).ShouldBe(typeof(SampleEvent));
		}
	}

	// ---- GetTypeName ----

	[Fact]
	public void GetTypeName_ForCachedType_ReturnsDeclaredName()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		var name = MessageTypeCache.GetTypeName(typeof(SampleEvent));

		name.ShouldBe("cache.event");
	}

	[Fact]
	public void GetTypeName_ForFallbackType_ReturnsDeclaredName()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		// First call creates fallback entry
		MessageTypeCache.GetOrCreateMetadata(typeof(SampleEvent));
		var name = MessageTypeCache.GetTypeName(typeof(SampleEvent));

		name.ShouldBe("cache.event");
	}

	[Fact]
	public void GetTypeName_ForUncachedType_ReturnsDeclaredName()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		var name = MessageTypeCache.GetTypeName(typeof(SampleCommand));

		name.ShouldBe("cache.command");
	}

	[Fact]
	public void GetTypeName_ForNullType_Throws()
	{
		Should.Throw<ArgumentNullException>(() => MessageTypeCache.GetTypeName(null!));
	}

	// ---- IsCached ----

	[Fact]
	public void IsCached_ForRegisteredType_ReturnsTrue()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		MessageTypeCache.IsCached(typeof(SampleEvent)).ShouldBeTrue();
	}

	[Fact]
	public void IsCached_ForUnregisteredType_ReturnsFalse()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent)]);

		MessageTypeCache.IsCached(typeof(SampleCommand)).ShouldBeFalse();
	}

	[Fact]
	public void IsCached_DoesNotCountFallbackEntries()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		// Create a fallback entry
		MessageTypeCache.GetOrCreateMetadata(typeof(SampleEvent));

		// IsCached only checks the frozen dictionary, not fallback
		MessageTypeCache.IsCached(typeof(SampleEvent)).ShouldBeFalse();
	}

	// ---- GetCachedTypes ----

	[Fact]
	public void GetCachedTypes_ReturnsAllRegisteredTypes()
	{
		MessageTypeCache.Initialize([typeof(SampleEvent), typeof(SampleCommand)]);

		var types = MessageTypeCache.GetCachedTypes();

		types.Count.ShouldBe(2);
		types.ShouldContain(typeof(SampleEvent));
		types.ShouldContain(typeof(SampleCommand));
	}

	[Fact]
	public void GetCachedTypes_WhenEmpty_ReturnsEmpty()
	{
		MessageTypeCache.Initialize(Array.Empty<Type>());

		MessageTypeCache.GetCachedTypes().ShouldBeEmpty();
	}

	// ---- Concurrent Initialize ----

	[Fact]
	public void Initialize_ConcurrentCalls_OnlyFirstSucceeds()
	{
		var initCount = 0;

		Parallel.For(0, 10, _ =>
		{
			// Each thread tries to initialize with a unique set
			MessageTypeCache.Initialize([typeof(SampleEvent)]);
			Interlocked.Increment(ref initCount);
		});

		// All calls should complete without error
		initCount.ShouldBe(10);
		// Cache should contain the types from whichever call won
		MessageTypeCache.IsCached(typeof(SampleEvent)).ShouldBeTrue();
	}

	// ---- Integration: routing hint consistency ----

	[Fact]
	public void CachedMetadata_HasCorrectRoutingHints()
	{
		MessageTypeCache.Initialize([
			typeof(SampleEvent),
			typeof(SampleCommand),
			typeof(SampleIntegrationEvent)
		]);

		MessageTypeCache.GetMetadata(typeof(SampleEvent))!.RoutingHint.ShouldBe("local");
		MessageTypeCache.GetMetadata(typeof(SampleCommand))!.RoutingHint.ShouldBe("default");
		MessageTypeCache.GetMetadata(typeof(SampleIntegrationEvent))!.RoutingHint.ShouldBe("remote");
	}

	// ---- Helpers ----

	private static void ResetCache() => MessageTypeCache.Reset();

	// ---- Test doubles ----

	[MessageName("cache.event")]
	[MessageNameAlias("cache.legacy-event")]
	private sealed class SampleEvent : IDispatchEvent;

	[MessageName("cache.command")]
	private sealed class SampleCommand : IDispatchAction;

	private sealed class SampleIntegrationEvent : IIntegrationEvent;

	private sealed class FallbackType;
	[MessageName("collision")]
	private sealed class CanonicalA;
	[MessageName("collision")]
	private sealed class CanonicalB;
	[MessageName("alias-a"), MessageNameAlias("collision")]
	private sealed class AliasA;
	[MessageName("alias-b"), MessageNameAlias("collision")]
	private sealed class AliasB;

}
