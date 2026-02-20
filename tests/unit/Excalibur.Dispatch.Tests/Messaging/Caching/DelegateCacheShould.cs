// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DelegateCacheShould
{
	public DelegateCacheShould()
	{
		DelegateCache.Clear();
	}

	// --- DelegateCacheKey ---

	[Fact]
	public void DelegateCacheKey_TwoArgConstructor_SetsProperties()
	{
		// Act
		var key = new DelegateCacheKey("prefix", "mykey");

		// Assert
		key.Prefix.ShouldBe("prefix");
		key.Key.ShouldBe("mykey");
		key.Type1.ShouldBeNull();
		key.Type2.ShouldBeNull();
	}

	[Fact]
	public void DelegateCacheKey_ThreeArgConstructor_SetsProperties()
	{
		// Act
		var key = new DelegateCacheKey("prefix", "mykey", typeof(int));

		// Assert
		key.Prefix.ShouldBe("prefix");
		key.Key.ShouldBe("mykey");
		key.Type1.ShouldBe(typeof(int));
		key.Type2.ShouldBeNull();
	}

	[Fact]
	public void DelegateCacheKey_FourArgConstructor_SetsProperties()
	{
		// Act
		var key = new DelegateCacheKey("prefix", "mykey", typeof(int), typeof(string));

		// Assert
		key.Prefix.ShouldBe("prefix");
		key.Key.ShouldBe("mykey");
		key.Type1.ShouldBe(typeof(int));
		key.Type2.ShouldBe(typeof(string));
	}

	[Fact]
	public void DelegateCacheKey_SameValues_AreEqual()
	{
		// Arrange
		var key1 = new DelegateCacheKey("p", "k", typeof(int));
		var key2 = new DelegateCacheKey("p", "k", typeof(int));

		// Assert
		key1.Equals(key2).ShouldBeTrue();
		(key1 == key2).ShouldBeTrue();
		(key1 != key2).ShouldBeFalse();
		key1.GetHashCode().ShouldBe(key2.GetHashCode());
	}

	[Fact]
	public void DelegateCacheKey_DifferentValues_AreNotEqual()
	{
		// Arrange
		var key1 = new DelegateCacheKey("p", "k1");
		var key2 = new DelegateCacheKey("p", "k2");

		// Assert
		key1.Equals(key2).ShouldBeFalse();
		(key1 != key2).ShouldBeTrue();
	}

	[Fact]
	public void DelegateCacheKey_Equals_WithObject()
	{
		// Arrange
		var key = new DelegateCacheKey("p", "k");
		object boxed = new DelegateCacheKey("p", "k");

		// Assert
		key.Equals(boxed).ShouldBeTrue();
		key.Equals("not a key").ShouldBeFalse();
		key.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void DelegateCacheKey_ToString_TwoArgs()
	{
		// Act
		var key = new DelegateCacheKey("continuation", "mykey");

		// Assert
		key.ToString().ShouldBe("continuation_mykey");
	}

	[Fact]
	public void DelegateCacheKey_ToString_ThreeArgs()
	{
		// Act
		var key = new DelegateCacheKey("continuation", "mykey", typeof(int));

		// Assert
		key.ToString().ShouldBe("continuation_mykey_Int32");
	}

	[Fact]
	public void DelegateCacheKey_ToString_FourArgs()
	{
		// Act
		var key = new DelegateCacheKey("continuation", "mykey", typeof(int), typeof(string));

		// Assert
		key.ToString().ShouldBe("continuation_mykey_Int32_String");
	}

	// --- DelegateCache ---

	[Fact]
	public void GetOrCreate_WithStringKey_CachesDelegate()
	{
		// Arrange
		var callCount = 0;
		Func<Action> factory = () => { callCount++; return () => { }; };

		// Act
		var result1 = DelegateCache.GetOrCreate("test-key", factory);
		var result2 = DelegateCache.GetOrCreate("test-key", factory);

		// Assert
		result1.ShouldNotBeNull();
		result2.ShouldBe(result1);
		callCount.ShouldBe(1); // Factory called only once
	}

	[Fact]
	public void GetOrCreate_WithStructKey_CachesDelegate()
	{
		// Arrange
		var callCount = 0;
		var key = new DelegateCacheKey("test", "key1");
		Func<Action> factory = () => { callCount++; return () => { }; };

		// Act
		var result1 = DelegateCache.GetOrCreate(key, factory);
		var result2 = DelegateCache.GetOrCreate(key, factory);

		// Assert
		result1.ShouldNotBeNull();
		result2.ShouldBe(result1);
		callCount.ShouldBe(1);
	}

	[Fact]
	public void GetOrCreate_WithNullFactory_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			DelegateCache.GetOrCreate<Action>("key", null!));

		Should.Throw<ArgumentNullException>(() =>
			DelegateCache.GetOrCreate<Action>(new DelegateCacheKey("p", "k"), null!));
	}

	[Fact]
	public void GetAsyncAction_CachesDelegate()
	{
		// Arrange
		Func<Task> action = () => Task.CompletedTask;

		// Act
		var result = DelegateCache.GetAsyncAction("async-key", action);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetAsyncAction_Generic_CachesDelegate()
	{
		// Arrange
		Func<string, Task> action = _ => Task.CompletedTask;

		// Act
		var result = DelegateCache.GetAsyncAction("async-key-generic", action);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetValueTaskAction_CachesDelegate()
	{
		// Arrange
		Func<ValueTask> action = () => ValueTask.CompletedTask;

		// Act
		var result = DelegateCache.GetValueTaskAction("vt-key", action);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetStatistics_ReturnsValidCounts()
	{
		// Arrange
		DelegateCache.GetOrCreate("stat-key", () => new Action(() => { }));
		DelegateCache.GetOrCreate("stat-key", () => new Action(() => { })); // hit

		// Act
		var (hits, misses, cacheSize) = DelegateCache.GetStatistics();

		// Assert
		hits.ShouldBeGreaterThanOrEqualTo(1);
		misses.ShouldBeGreaterThanOrEqualTo(1);
		cacheSize.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void GetDetailedStatistics_ReturnsBreakdown()
	{
		// Arrange
		DelegateCache.GetOrCreate("detail-key", () => new Action(() => { }));
		DelegateCache.GetOrCreate(new DelegateCacheKey("p", "k"), () => new Action(() => { }));

		// Act
		var (hits, misses, stringSize, structSize) = DelegateCache.GetDetailedStatistics();

		// Assert
		stringSize.ShouldBeGreaterThanOrEqualTo(1);
		structSize.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void Clear_RemovesAllEntries()
	{
		// Arrange
		DelegateCache.GetOrCreate("clear-key", () => new Action(() => { }));
		DelegateCache.GetOrCreate(new DelegateCacheKey("p", "k"), () => new Action(() => { }));

		// Act
		DelegateCache.Clear();
		var (_, _, cacheSize) = DelegateCache.GetStatistics();

		// Assert
		cacheSize.ShouldBe(0);
	}

	// --- DelegateCacheExtensions ---

	[Fact]
	public void GetContinuation_ReturnsDelegate()
	{
		// Arrange
		Func<int, string> selector = i => i.ToString();

		// Act
		var result = "cont-key".GetContinuation(selector);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetErrorHandler_ReturnsDelegate()
	{
		// Arrange
		Func<Exception, bool> predicate = _ => true;

		// Act
		var result = "err-key".GetErrorHandler(predicate);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public void GetTransform_ReturnsDelegate()
	{
		// Arrange
		Func<int, string> transform = i => i.ToString();

		// Act
		var result = "trans-key".GetTransform(transform);

		// Assert
		result.ShouldNotBeNull();
		result(42).ShouldBe("42");
	}

	// --- CommonDelegates ---

	[Fact]
	public void CommonDelegates_CompletedTask_ReturnsCompletedTask()
	{
		// Act
		var result = CommonDelegates.CompletedTask();

		// Assert
		result.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public void CommonDelegates_CompletedTaskWithString_ReturnsCompletedTask()
	{
		// Act
		var result = CommonDelegates.CompletedTaskWithString("test");

		// Assert
		result.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public void CommonDelegates_AlwaysTrue_ReturnsTrue()
	{
		// Assert
		CommonDelegates.AlwaysTrue(null).ShouldBeTrue();
		CommonDelegates.AlwaysTrue("anything").ShouldBeTrue();
	}

	[Fact]
	public void CommonDelegates_AlwaysFalse_ReturnsFalse()
	{
		// Assert
		CommonDelegates.AlwaysFalse(null).ShouldBeFalse();
		CommonDelegates.AlwaysFalse("anything").ShouldBeFalse();
	}

	[Fact]
	public void CommonDelegates_NoOp_DoesNotThrow()
	{
		// Act & Assert
		CommonDelegates.NoOp();
		CommonDelegates.NoOpWithParam("test");
	}

	[Fact]
	public void CommonDelegates_CompletedTaskWithCancellation_ReturnsCompleted()
	{
		// Act
		var result = CommonDelegates.CompletedTaskWithCancellation(CancellationToken.None);

		// Assert
		result.IsCompleted.ShouldBeTrue();
	}

	[Fact]
	public void CommonDelegates_AlwaysCatchException_ReturnsTrue()
	{
		// Assert
		CommonDelegates.AlwaysCatchException(new InvalidOperationException()).ShouldBeTrue();
	}

	// --- MessageHandlerDelegateCache<T> ---

	[Fact]
	public void MessageHandlerDelegateCache_GetAsyncHandler_CachesDelegate()
	{
		// Arrange
		var cache = new MessageHandlerDelegateCache<string>();
		var callCount = 0;

		// Act
		var handler1 = cache.GetAsyncHandler(typeof(string), () =>
		{
			callCount++;
			return _ => Task.CompletedTask;
		});
		var handler2 = cache.GetAsyncHandler(typeof(string), () =>
		{
			callCount++;
			return _ => Task.CompletedTask;
		});

		// Assert
		handler1.ShouldBe(handler2);
		callCount.ShouldBe(1);
	}

	[Fact]
	public void MessageHandlerDelegateCache_GetValueTaskHandler_CachesDelegate()
	{
		// Arrange
		var cache = new MessageHandlerDelegateCache<string>();

		// Act
		var handler = cache.GetValueTaskHandler(typeof(int), () => _ => ValueTask.CompletedTask);

		// Assert
		handler.ShouldNotBeNull();
	}

	[Fact]
	public void MessageHandlerDelegateCache_GetSyncHandler_CachesDelegate()
	{
		// Arrange
		var cache = new MessageHandlerDelegateCache<string>();

		// Act
		var handler = cache.GetSyncHandler(typeof(int), () => _ => { });

		// Assert
		handler.ShouldNotBeNull();
	}

	[Fact]
	public void MessageHandlerDelegateCache_Clear_RemovesAll()
	{
		// Arrange
		var cache = new MessageHandlerDelegateCache<string>();
		cache.GetAsyncHandler(typeof(string), () => _ => Task.CompletedTask);
		cache.GetValueTaskHandler(typeof(string), () => _ => ValueTask.CompletedTask);
		cache.GetSyncHandler(typeof(string), () => _ => { });

		// Act
		cache.Clear();

		// Assert - new factory should be called after clear
		var callCount = 0;
		cache.GetAsyncHandler(typeof(string), () =>
		{
			callCount++;
			return _ => Task.CompletedTask;
		});
		callCount.ShouldBe(1);
	}
}
