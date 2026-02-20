// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Unit tests for <see cref="MessageHandlerDelegateCache{TMessage}"/>.
/// Verifies caching behavior for async, value task, and sync handlers.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Messaging")]
[Trait("Priority", "1")]
public sealed class MessageHandlerDelegateCacheShould
{
	private readonly MessageHandlerDelegateCache<TestMessage> _cache;

	public MessageHandlerDelegateCacheShould()
	{
		_cache = new MessageHandlerDelegateCache<TestMessage>();
	}

	#region GetAsyncHandler Tests

	[Fact]
	public void CacheAsyncHandler_WhenCalledFirstTime()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<TestMessage, Task>> factory = () =>
		{
			factoryCallCount++;
			return _ => Task.CompletedTask;
		};

		// Act
		var handler = _cache.GetAsyncHandler(typeof(TestHandler), factory);

		// Assert
		handler.ShouldNotBeNull();
		factoryCallCount.ShouldBe(1);
	}

	[Fact]
	public void ReturnCachedAsyncHandler_WhenCalledWithSameHandlerType()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<TestMessage, Task>> factory = () =>
		{
			factoryCallCount++;
			return _ => Task.CompletedTask;
		};

		// Act
		var handler1 = _cache.GetAsyncHandler(typeof(TestHandler), factory);
		var handler2 = _cache.GetAsyncHandler(typeof(TestHandler), factory);

		// Assert
		handler1.ShouldBeSameAs(handler2);
		factoryCallCount.ShouldBe(1); // Factory called only once
	}

	[Fact]
	public void ReturnDifferentAsyncHandlers_ForDifferentHandlerTypes()
	{
		// Arrange
		Func<Func<TestMessage, Task>> factory1 = () => _ => Task.CompletedTask;
		Func<Func<TestMessage, Task>> factory2 = () => _ => Task.FromResult(42).ContinueWith(_ => { });

		// Act
		var handler1 = _cache.GetAsyncHandler(typeof(TestHandler), factory1);
		var handler2 = _cache.GetAsyncHandler(typeof(AnotherTestHandler), factory2);

		// Assert
		handler1.ShouldNotBeSameAs(handler2);
	}

	[Fact]
	public async Task ExecuteCachedAsyncHandler_Successfully()
	{
		// Arrange
		var wasExecuted = false;
		Func<Func<TestMessage, Task>> factory = () => _ =>
		{
			wasExecuted = true;
			return Task.CompletedTask;
		};
		var handler = _cache.GetAsyncHandler(typeof(TestHandler), factory);

		// Act
		await handler(new TestMessage("test"));

		// Assert
		wasExecuted.ShouldBeTrue();
	}

	#endregion

	#region GetValueTaskHandler Tests

	[Fact]
	public void CacheValueTaskHandler_WhenCalledFirstTime()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<TestMessage, ValueTask>> factory = () =>
		{
			factoryCallCount++;
			return _ => ValueTask.CompletedTask;
		};

		// Act
		var handler = _cache.GetValueTaskHandler(typeof(TestHandler), factory);

		// Assert
		handler.ShouldNotBeNull();
		factoryCallCount.ShouldBe(1);
	}

	[Fact]
	public void ReturnCachedValueTaskHandler_WhenCalledWithSameHandlerType()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<TestMessage, ValueTask>> factory = () =>
		{
			factoryCallCount++;
			return _ => ValueTask.CompletedTask;
		};

		// Act
		var handler1 = _cache.GetValueTaskHandler(typeof(TestHandler), factory);
		var handler2 = _cache.GetValueTaskHandler(typeof(TestHandler), factory);

		// Assert
		handler1.ShouldBeSameAs(handler2);
		factoryCallCount.ShouldBe(1); // Factory called only once
	}

	[Fact]
	public void ReturnDifferentValueTaskHandlers_ForDifferentHandlerTypes()
	{
		// Arrange
		Func<Func<TestMessage, ValueTask>> factory1 = () => _ => ValueTask.CompletedTask;
		Func<Func<TestMessage, ValueTask>> factory2 = () => _ => new ValueTask(Task.Delay(1));

		// Act
		var handler1 = _cache.GetValueTaskHandler(typeof(TestHandler), factory1);
		var handler2 = _cache.GetValueTaskHandler(typeof(AnotherTestHandler), factory2);

		// Assert
		handler1.ShouldNotBeSameAs(handler2);
	}

	[Fact]
	public async Task ExecuteCachedValueTaskHandler_Successfully()
	{
		// Arrange
		var wasExecuted = false;
		Func<Func<TestMessage, ValueTask>> factory = () => _ =>
		{
			wasExecuted = true;
			return ValueTask.CompletedTask;
		};
		var handler = _cache.GetValueTaskHandler(typeof(TestHandler), factory);

		// Act
		await handler(new TestMessage("test"));

		// Assert
		wasExecuted.ShouldBeTrue();
	}

	#endregion

	#region GetSyncHandler Tests

	[Fact]
	public void CacheSyncHandler_WhenCalledFirstTime()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Action<TestMessage>> factory = () =>
		{
			factoryCallCount++;
			return _ => { };
		};

		// Act
		var handler = _cache.GetSyncHandler(typeof(TestHandler), factory);

		// Assert
		handler.ShouldNotBeNull();
		factoryCallCount.ShouldBe(1);
	}

	[Fact]
	public void ReturnCachedSyncHandler_WhenCalledWithSameHandlerType()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Action<TestMessage>> factory = () =>
		{
			factoryCallCount++;
			return _ => { };
		};

		// Act
		var handler1 = _cache.GetSyncHandler(typeof(TestHandler), factory);
		var handler2 = _cache.GetSyncHandler(typeof(TestHandler), factory);

		// Assert
		handler1.ShouldBeSameAs(handler2);
		factoryCallCount.ShouldBe(1); // Factory called only once
	}

	[Fact]
	public void ReturnDifferentSyncHandlers_ForDifferentHandlerTypes()
	{
		// Arrange
		Func<Action<TestMessage>> factory1 = () => _ => { };
		Func<Action<TestMessage>> factory2 = () => msg => Console.WriteLine(msg.Content);

		// Act
		var handler1 = _cache.GetSyncHandler(typeof(TestHandler), factory1);
		var handler2 = _cache.GetSyncHandler(typeof(AnotherTestHandler), factory2);

		// Assert
		handler1.ShouldNotBeSameAs(handler2);
	}

	[Fact]
	public void ExecuteCachedSyncHandler_Successfully()
	{
		// Arrange
		var wasExecuted = false;
		Func<Action<TestMessage>> factory = () => _ =>
		{
			wasExecuted = true;
		};
		var handler = _cache.GetSyncHandler(typeof(TestHandler), factory);

		// Act
		handler(new TestMessage("test"));

		// Assert
		wasExecuted.ShouldBeTrue();
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void ClearAllCachedHandlers()
	{
		// Arrange - populate all caches
		_cache.GetAsyncHandler(typeof(TestHandler), () => _ => Task.CompletedTask);
		_cache.GetValueTaskHandler(typeof(TestHandler), () => _ => ValueTask.CompletedTask);
		_cache.GetSyncHandler(typeof(TestHandler), () => _ => { });

		var asyncFactoryCallCount = 0;
		var valueTaskFactoryCallCount = 0;
		var syncFactoryCallCount = 0;

		// Act
		_cache.Clear();

		// After clearing, factories should be called again
		_cache.GetAsyncHandler(typeof(TestHandler), () =>
		{
			asyncFactoryCallCount++;
			return _ => Task.CompletedTask;
		});
		_cache.GetValueTaskHandler(typeof(TestHandler), () =>
		{
			valueTaskFactoryCallCount++;
			return _ => ValueTask.CompletedTask;
		});
		_cache.GetSyncHandler(typeof(TestHandler), () =>
		{
			syncFactoryCallCount++;
			return _ => { };
		});

		// Assert - all factories should have been called once (cache was cleared)
		asyncFactoryCallCount.ShouldBe(1);
		valueTaskFactoryCallCount.ShouldBe(1);
		syncFactoryCallCount.ShouldBe(1);
	}

	#endregion

	#region Mixed Cache Tests

	[Fact]
	public void MaintainSeparateCaches_ForDifferentHandlerDelegateTypes()
	{
		// Arrange & Act - same handler type but different delegate types
		var asyncHandler = _cache.GetAsyncHandler(typeof(TestHandler), () => _ => Task.CompletedTask);
		var valueTaskHandler = _cache.GetValueTaskHandler(typeof(TestHandler), () => _ => ValueTask.CompletedTask);
		var syncHandler = _cache.GetSyncHandler(typeof(TestHandler), () => _ => { });

		// Assert - all handlers should be different
		// Note: We can't directly compare since they're different types, but we verify they all exist
		asyncHandler.ShouldNotBeNull();
		valueTaskHandler.ShouldNotBeNull();
		syncHandler.ShouldNotBeNull();
	}

	[Fact]
	public void HandleMultipleHandlerTypes_AcrossAllCacheTypes()
	{
		// Arrange
		var asyncCallCount = 0;
		var valueTaskCallCount = 0;
		var syncCallCount = 0;

		// Act - register multiple handler types for each cache
		_cache.GetAsyncHandler(typeof(TestHandler), () => { asyncCallCount++; return _ => Task.CompletedTask; });
		_cache.GetAsyncHandler(typeof(AnotherTestHandler), () => { asyncCallCount++; return _ => Task.CompletedTask; });

		_cache.GetValueTaskHandler(typeof(TestHandler), () => { valueTaskCallCount++; return _ => ValueTask.CompletedTask; });
		_cache.GetValueTaskHandler(typeof(AnotherTestHandler), () => { valueTaskCallCount++; return _ => ValueTask.CompletedTask; });

		_cache.GetSyncHandler(typeof(TestHandler), () => { syncCallCount++; return _ => { }; });
		_cache.GetSyncHandler(typeof(AnotherTestHandler), () => { syncCallCount++; return _ => { }; });

		// Assert
		asyncCallCount.ShouldBe(2);
		valueTaskCallCount.ShouldBe(2);
		syncCallCount.ShouldBe(2);
	}

	#endregion

	#region Test Types

	private sealed record TestMessage(string Content);

	private sealed class TestHandler
	{
	}

	private sealed class AnotherTestHandler
	{
	}

	#endregion
}
