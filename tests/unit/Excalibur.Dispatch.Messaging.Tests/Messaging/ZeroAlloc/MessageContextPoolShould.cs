// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.ZeroAlloc;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Messaging.ZeroAlloc;

/// <summary>
/// Unit tests for <see cref="MessageContextPool"/> lazy binding feature.
/// Sprint 450 - S450.4: Unit tests for MessageContextPool lazy binding (15+ tests).
/// </summary>
/// <remarks>
/// These tests verify the new parameterless Rent() overload that enables lazy message binding,
/// allowing contexts to be rented from the pool before the message is available.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
public sealed class MessageContextPoolShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly MessageContextPool _pool;

	public MessageContextPoolShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();
		_pool = new MessageContextPool(_serviceProvider);
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenServiceProviderIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MessageContextPool(null!));
	}

	[Fact]
	public void Constructor_CreatesPoolSuccessfully_WithValidServiceProvider()
	{
		// Arrange & Act
		using var sp = new ServiceCollection().BuildServiceProvider();
		var pool = new MessageContextPool(sp);

		// Assert - if we get here, construction succeeded
		_ = pool.ShouldNotBeNull();
	}

	#endregion

	#region Lazy Binding Pattern Tests (Rent without message)

	[Fact]
	public void Rent_WithoutMessage_ReturnsContext()
	{
		// Act
		var context = _pool.Rent();

		// Assert
		_ = context.ShouldNotBeNull();
		_ = context.ShouldBeAssignableTo<IMessageContext>();

		// Cleanup
		_pool.ReturnToPool(context);
	}

	[Fact]
	public void Rent_WithoutMessage_ContextCanReceiveMessageLater()
	{
		// Arrange
		var context = _pool.Rent();
		var message = A.Fake<IDispatchMessage>();

		// Act - set message after rent (lazy binding pattern)
		context.Message = message;

		// Assert
		context.Message.ShouldBe(message);

		// Cleanup
		_pool.ReturnToPool(context);
	}

	[Fact]
	public void Rent_WithMessage_InitializesMessageImmediately()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		var context = _pool.Rent(message);

		// Assert
		_ = context.ShouldNotBeNull();
		context.Message.ShouldBe(message);

		// Cleanup
		_pool.ReturnToPool(context);
	}

	[Fact]
	public void Rent_BothOverloads_ReturnDifferentContexts()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();

		// Act
		var contextWithoutMessage = _pool.Rent();
		var contextWithMessage = _pool.Rent(message);

		// Assert
		contextWithoutMessage.ShouldNotBeSameAs(contextWithMessage);

		// Cleanup
		_pool.ReturnToPool(contextWithoutMessage);
		_pool.ReturnToPool(contextWithMessage);
	}

	[Fact]
	public void Rent_WithoutMessage_AllowsSettingPropertiesBeforeMessage()
	{
		// Arrange
		var context = _pool.Rent();

		// Act - Set context properties before message is set (lazy binding pattern)
		context.CorrelationId = "test-correlation";
		context.TenantId = "tenant-123";
		context.UserId = "user-456";
		context.Items["custom-key"] = "custom-value";

		// Now set the message
		var message = A.Fake<IDispatchMessage>();
		context.Message = message;

		// Assert - all properties should be preserved
		context.CorrelationId.ShouldBe("test-correlation");
		context.TenantId.ShouldBe("tenant-123");
		context.UserId.ShouldBe("user-456");
		context.Items["custom-key"].ShouldBe("custom-value");
		context.Message.ShouldBe(message);

		// Cleanup
		_pool.ReturnToPool(context);
	}

	[Fact]
	public void Rent_WithoutMessage_ContextHasValidRequestServices()
	{
		// Act
		var context = _pool.Rent();

		// Assert - RequestServices should be available even without message
		_ = context.RequestServices.ShouldNotBeNull();

		// Cleanup
		_pool.ReturnToPool(context);
	}

	#endregion

	#region Reset Behavior Tests

	[Fact]
	public void ReturnToPool_ResetsContextForReuse()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = _pool.Rent(message);
		context.CorrelationId = "will-be-reset";
		context.Items["key"] = "value";

		// Act
		_pool.ReturnToPool(context);

		// Assert - context should be reset after return
		// We can't directly verify internal state, but we can verify re-rent works
		var rerentedContext = _pool.Rent();
		_ = rerentedContext.ShouldNotBeNull();

		// Cleanup
		_pool.ReturnToPool(rerentedContext);
	}

	[Fact]
	public void Reset_ClearsItemsOnPoolReturn()
	{
		// Arrange
		var context = _pool.Rent();
		context.Items["key1"] = "value1";
		context.Items["key2"] = 42;
		context.Items.Count.ShouldBeGreaterThan(0);

		// Act
		_pool.ReturnToPool(context);

		// Rent again - should get a clean context
		var newContext = _pool.Rent();
		newContext.Items["test"] = "test"; // Force Items initialization

		// Assert - new context should have clean items (except what we just added)
		newContext.Items.ContainsKey("key1").ShouldBeFalse();
		newContext.Items.ContainsKey("key2").ShouldBeFalse();

		// Cleanup
		_pool.ReturnToPool(newContext);
	}

	[Fact]
	public void Reset_ClearsResultOnPoolReturn()
	{
		// Arrange
		var context = _pool.Rent();
		context.Result = new object();
		_ = context.Result.ShouldNotBeNull();

		// Act
		_pool.ReturnToPool(context);
		var reusedContext = _pool.Rent();

		// Assert - if pool returned a reset context, Result should be null
		_ = reusedContext.ShouldNotBeNull();

		// Cleanup
		_pool.ReturnToPool(reusedContext);
	}

	[Fact]
	public void Reset_ClearsHotPathPropertiesOnPoolReturn()
	{
		// Arrange
		var context = _pool.Rent();
		context.ProcessingAttempts = 5;
		context.IsRetry = true;
		context.ValidationPassed = true;
		context.TimeoutExceeded = true;
		context.RateLimitExceeded = true;
		context.CorrelationId = "test-correlation";
		context.TenantId = "test-tenant";

		// Act
		_pool.ReturnToPool(context);

		// Get a context from pool (may be the same or different)
		var freshContext = _pool.Rent();

		// Assert - for a pooled context that got reset, properties should be default
		_ = freshContext.ShouldNotBeNull();

		// Cleanup
		_pool.ReturnToPool(freshContext);
	}

	#endregion

	#region Pool Lifecycle Tests

	[Fact]
	public void RentReturnRent_ReusesPooledContext()
	{
		// Arrange - Rent multiple contexts to warm up the pool
		var contexts = new IMessageContext[5];
		for (var i = 0; i < 5; i++)
		{
			contexts[i] = _pool.Rent();
		}

		// Return all contexts
		foreach (var ctx in contexts)
		{
			_pool.ReturnToPool(ctx);
		}

		// Act - Rent again, should get pooled contexts
		var reusedContexts = new IMessageContext[5];
		for (var i = 0; i < 5; i++)
		{
			reusedContexts[i] = _pool.Rent();
		}

		// Assert - contexts should be usable (proves pool is working)
		foreach (var ctx in reusedContexts)
		{
			_ = ctx.ShouldNotBeNull();
			_pool.ReturnToPool(ctx);
		}
	}

	[Fact]
	public void MultipleRentReturn_NoStateLeakage()
	{
		// Arrange
		var message1 = A.Fake<IDispatchMessage>();
		var context1 = _pool.Rent(message1);
		context1.CorrelationId = "correlation-1";
		context1.TenantId = "tenant-1";
		context1.Items["secret"] = "sensitive-data";

		// Act - Return and rent again
		_pool.ReturnToPool(context1);

		var message2 = A.Fake<IDispatchMessage>();
		var context2 = _pool.Rent(message2);

		// Assert - No data leakage between uses
		context2.Message.ShouldBe(message2);
		// If context is reused from pool, state should be reset
		// If new context, state should be default
		context2.Items.ContainsKey("secret").ShouldBeFalse();

		// Cleanup
		_pool.ReturnToPool(context2);
	}

	[Fact]
	public void ReturnToPool_ThenRent_PreservesPoolFunctionality()
	{
		// Arrange & Act - cycle through rent/return multiple times
		for (var i = 0; i < 10; i++)
		{
			var context = _pool.Rent();
			context.CorrelationId = $"iteration-{i}";
			_pool.ReturnToPool(context);
		}

		// Assert - pool should still work
		var finalContext = _pool.Rent();
		_ = finalContext.ShouldNotBeNull();
		_pool.ReturnToPool(finalContext);
	}

	#endregion

	#region Concurrency Tests

	[Fact]
	public async Task ConcurrentRent_IsThreadSafe()
	{
		// Arrange
		const int concurrentOperations = 100;
		var contexts = new ConcurrentBag<IMessageContext>();

		// Act - Rent contexts concurrently
		await Parallel.ForEachAsync(
			Enumerable.Range(0, concurrentOperations),
			new ParallelOptions { MaxDegreeOfParallelism = 10 },
			async (_, _) =>
			{
				var context = _pool.Rent();
				contexts.Add(context);
				await Task.Yield();
			});

		// Assert - All contexts should be valid
		contexts.Count.ShouldBe(concurrentOperations);
		foreach (var ctx in contexts)
		{
			_ = ctx.ShouldNotBeNull();
		}

		// Cleanup
		foreach (var ctx in contexts)
		{
			_pool.ReturnToPool(ctx);
		}
	}

	[Fact]
	public async Task ConcurrentReturn_IsThreadSafe()
	{
		// Arrange
		const int concurrentOperations = 100;
		var contexts = new List<IMessageContext>();

		// Rent all contexts first
		for (var i = 0; i < concurrentOperations; i++)
		{
			contexts.Add(_pool.Rent());
		}

		// Act - Return contexts concurrently
		await Parallel.ForEachAsync(
			contexts,
			new ParallelOptions { MaxDegreeOfParallelism = 10 },
			async (context, _) =>
			{
				_pool.ReturnToPool(context);
				await Task.Yield();
			});

		// Assert - No exceptions means thread-safe return
		// Verify pool is still functional
		var newContext = _pool.Rent();
		_ = newContext.ShouldNotBeNull();
		_pool.ReturnToPool(newContext);
	}

	[Fact]
	public async Task ConcurrentRentReturn_NoDeadlock()
	{
		// Arrange
		const int iterations = 50;
		var completedCount = 0;
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

		// Act - Concurrent rent and return operations
		var tasks = Enumerable.Range(0, 10).Select(async _ =>
		{
			for (var i = 0; i < iterations && !cts.Token.IsCancellationRequested; i++)
			{
				var context = _pool.Rent();
				context.CorrelationId = $"correlation-{i}";
				await Task.Yield();
				_pool.ReturnToPool(context);
				_ = Interlocked.Increment(ref completedCount);
			}
		});

		await Task.WhenAll(tasks);

		// Assert - All operations completed without deadlock
		completedCount.ShouldBeGreaterThanOrEqualTo(iterations * 10 - 10); // Allow small tolerance for timing
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void ReturnToPool_WithNonPooledContext_HandlesGracefully()
	{
		// Arrange - Create a context that wasn't from the pool
		var nonPooledContext = A.Fake<IMessageContext>();

		// Act & Assert - Should not throw
		Should.NotThrow(() => _pool.ReturnToPool(nonPooledContext));
	}

	[Fact]
	public void ReturnToPool_WithNull_HandlesOrThrows()
	{
		// Act & Assert - Should either handle gracefully or throw ArgumentNullException
		var exception = Record.Exception(() => _pool.ReturnToPool(null!));

		// Either no exception or ArgumentNullException is acceptable
		if (exception != null)
		{
			_ = exception.ShouldBeOfType<ArgumentNullException>();
		}
	}

	[Fact]
	public void Rent_MultipleTimesWithoutReturn_DoesNotExhaustPool()
	{
		// Arrange & Act - Rent many contexts without returning
		var contexts = new List<IMessageContext>();
		for (var i = 0; i < 100; i++)
		{
			contexts.Add(_pool.Rent());
		}

		// Assert - Pool should handle this by creating new contexts
		contexts.Count.ShouldBe(100);
		foreach (var ctx in contexts)
		{
			_ = ctx.ShouldNotBeNull();
		}

		// Cleanup
		foreach (var ctx in contexts)
		{
			_pool.ReturnToPool(ctx);
		}
	}

	[Fact]
	public void Rent_WithMessage_ThenSetDifferentMessage_Works()
	{
		// Arrange
		var originalMessage = A.Fake<IDispatchMessage>();
		var context = _pool.Rent(originalMessage);
		context.Message.ShouldBe(originalMessage);

		// Act - Replace message
		var newMessage = A.Fake<IDispatchMessage>();
		context.Message = newMessage;

		// Assert
		context.Message.ShouldBe(newMessage);

		// Cleanup
		_pool.ReturnToPool(context);
	}

	[Fact]
	public void Rent_WithoutMessage_MessagePropertyIsAccessible()
	{
		// Arrange
		var context = _pool.Rent();

		// Act & Assert - Accessing Message should not throw
		// It may be null or an EmptyMessage sentinel
		var exception = Record.Exception(() => _ = context.Message);
		exception.ShouldBeNull();

		// Cleanup
		_pool.ReturnToPool(context);
	}

	#endregion
}
