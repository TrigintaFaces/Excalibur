// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Collection definition for tests that share the static HandlerInvokerRegistry cache.
/// Using a collection ensures these tests run sequentially to avoid cache interference.
/// </summary>
[CollectionDefinition("HandlerInvokerRegistry")]
public class HandlerInvokerRegistryCollection;

/// <summary>
/// Unit tests for <see cref="HandlerInvokerRegistry"/>.
/// Sprint 451 - S451.5: Tests for PERF-4 ContinueWith elimination.
/// </summary>
/// <remarks>
/// These tests verify the async/await pattern used in CreateInvoker
/// instead of the previous ContinueWith approach, which eliminates
/// closure allocations when invoking handlers via reflection.
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
public sealed class HandlerInvokerRegistryShould : IDisposable
{
	public HandlerInvokerRegistryShould()
	{
		// Clear static cache before each test to ensure isolation from other test classes
		HandlerInvokerRegistry.ClearCache();
	}

	public void Dispose()
	{
		// Reset static cache after each test to prevent state pollution
		HandlerInvokerRegistry.ClearCache();
	}

	#region Manual Registration Tests

	[Fact]
	public void RegisterInvoker_WithVoidHandler_RegistersSuccessfully()
	{
		// Arrange & Act
		HandlerInvokerRegistry.RegisterInvoker<TestVoidHandler, TestMessage>(
			(handler, message, ct) => handler.HandleAsync(message, ct));

		// Assert
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(TestVoidHandler));
		_ = invoker.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterInvoker_WithResultHandler_RegistersSuccessfully()
	{
		// Arrange & Act
		HandlerInvokerRegistry.RegisterInvoker<TestResultHandler, TestMessage, string>(
			(handler, message, ct) => handler.HandleAsync(message, ct));

		// Assert
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(TestResultHandler));
		_ = invoker.ShouldNotBeNull();
	}

	[Fact]
	public async Task RegisteredInvoker_WithVoidHandler_ExecutesHandler()
	{
		// Arrange
		HandlerInvokerRegistry.RegisterInvoker<TestVoidHandler, TestMessage>(
			(handler, message, ct) => handler.HandleAsync(message, ct));

		var handler = new TestVoidHandler();
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(TestVoidHandler));

		// Act
		var result = await invoker(handler, message, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		handler.WasInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task RegisteredInvoker_WithResultHandler_ReturnsResult()
	{
		// Arrange
		HandlerInvokerRegistry.RegisterInvoker<TestResultHandler, TestMessage, string>(
			(handler, message, ct) => handler.HandleAsync(message, ct));

		var handler = new TestResultHandler { ResultToReturn = "test-result" };
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(TestResultHandler));

		// Act
		var result = await invoker(handler, message, CancellationToken.None);

		// Assert
		result.ShouldBe("test-result");
	}

	#endregion

	#region Dynamic Invoker Creation Tests

	[Fact]
	public void GetInvoker_WithUnregisteredHandler_CreatesInvokerDynamically()
	{
		// Arrange - Use a unique handler type that hasn't been registered

		// Act
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(DynamicVoidHandler));

		// Assert
		_ = invoker.ShouldNotBeNull();
	}

	[Fact]
	public async Task DynamicInvoker_WithVoidHandler_ExecutesHandler()
	{
		// Arrange
		var handler = new DynamicVoidHandler();
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(DynamicVoidHandler));

		// Act
		var result = await invoker(handler, message, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
		handler.WasInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task DynamicInvoker_WithResultHandler_ReturnsResult()
	{
		// Arrange
		var handler = new DynamicResultHandler { ResultToReturn = "dynamic-result" };
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(DynamicResultHandler));

		// Act
		var result = await invoker(handler, message, CancellationToken.None);

		// Assert
		result.ShouldBe("dynamic-result");
	}

	[Fact]
	public async Task DynamicInvoker_WithIntResultHandler_ReturnsIntResult()
	{
		// Arrange
		var handler = new DynamicIntResultHandler { ResultToReturn = 42 };
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(DynamicIntResultHandler));

		// Act
		var result = await invoker(handler, message, CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task DynamicInvoker_PropagatesCancellation()
	{
		// Arrange
		var handler = new CancellationAwareHandler();
		var message = new TestMessage();
		using var cts = new CancellationTokenSource();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(CancellationAwareHandler));

		// Act
		_ = await invoker(handler, message, cts.Token);

		// Assert
		handler.ReceivedToken.ShouldBe(cts.Token);
	}

	#endregion

	#region Caching Behavior Tests

	[Fact]
	public void GetInvoker_CalledMultipleTimes_ReturnsSameInvoker()
	{
		// Arrange & Act
		var invoker1 = HandlerInvokerRegistry.GetInvoker(typeof(CachedHandler));
		var invoker2 = HandlerInvokerRegistry.GetInvoker(typeof(CachedHandler));

		// Assert
		invoker1.ShouldBe(invoker2);
	}

	[Fact]
	public async Task GetInvoker_UnderConcurrentAccess_IsThreadSafe()
	{
		// Arrange
		const int concurrentOperations = 50;
		var invokers = new ConcurrentBag<Func<object, IDispatchMessage, CancellationToken, Task<object?>>>();

		// Pre-warm the cache with a single call to ensure deterministic behavior.
		// ConcurrentDictionary.GetOrAdd can invoke the factory multiple times during
		// concurrent races (only one value is stored, but multiple may be created).
		// By pre-warming, all concurrent calls will hit the cached value.
		var cachedInvoker = HandlerInvokerRegistry.GetInvoker(typeof(ThreadSafeHandler));
		cachedInvoker.ShouldNotBeNull();

		// Act
		await Parallel.ForEachAsync(
			Enumerable.Range(0, concurrentOperations),
			new ParallelOptions { MaxDegreeOfParallelism = 10 },
			async (_, _) =>
			{
				var invoker = HandlerInvokerRegistry.GetInvoker(typeof(ThreadSafeHandler));
				if (invoker != null)
				{
					invokers.Add(invoker);
				}

				await Task.Yield();
			});

		// Assert - All should return the same cached invoker
		invokers.Count.ShouldBe(concurrentOperations);
		invokers.All(i => ReferenceEquals(i, cachedInvoker)).ShouldBeTrue();
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task DynamicInvoker_PropagatesExceptionFromHandler()
	{
		// Arrange
		var handler = new ThrowingHandler { ExceptionToThrow = new InvalidOperationException("Handler error") };
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(ThrowingHandler));

		// Act & Assert
		// Since reflection is used, the exception is wrapped in TargetInvocationException
		var ex = await Should.ThrowAsync<System.Reflection.TargetInvocationException>(() =>
			invoker(handler, message, CancellationToken.None));

		_ = ex.InnerException.ShouldBeOfType<InvalidOperationException>();
		ex.InnerException.Message.ShouldBe("Handler error");
	}

	[Fact]
	public void GetInvoker_WithNoHandleAsyncMethod_ThrowsInvalidOperation()
	{
		// Arrange & Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			HandlerInvokerRegistry.GetInvoker(typeof(InvalidHandler)));

		ex.Message.ShouldContain("HandleAsync");
	}

	[Fact]
	public void GetInvoker_WithWrongParameterCount_ThrowsInvalidOperation()
	{
		// Arrange & Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			HandlerInvokerRegistry.GetInvoker(typeof(WrongParameterCountHandler)));

		ex.Message.ShouldContain("exactly 2 parameters");
	}

	#endregion

	#region Async/Await Pattern Verification Tests

	[Fact]
	public async Task DynamicInvoker_UsesAsyncAwait_NotContinueWith()
	{
		// Arrange - This test verifies the behavior that async/await provides
		// by checking that the invoker properly awaits the handler
		var handler = new DelayedHandler { DelayMs = 50 };
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(DelayedHandler));

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		_ = await invoker(handler, message, CancellationToken.None);
		stopwatch.Stop();

		// Assert - The await should have completed after the delay
		stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(40); // Allow some variance
		handler.WasInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task DynamicInvoker_WithDelayedResultHandler_AwaitsProperly()
	{
		// Arrange
		var handler = new DelayedResultHandler { DelayMs = 50, ResultToReturn = "delayed-result" };
		var message = new TestMessage();
		var invoker = HandlerInvokerRegistry.GetInvoker(typeof(DelayedResultHandler));

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var result = await invoker(handler, message, CancellationToken.None);
		stopwatch.Stop();

		// Assert
		stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(40);
		result.ShouldBe("delayed-result");
	}

	#endregion

	#region Test Fixtures

	private sealed class TestMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestVoidHandler
	{
		public bool WasInvoked { get; private set; }

		public Task HandleAsync(TestMessage message, CancellationToken ct)
		{
			WasInvoked = true;
			return Task.CompletedTask;
		}
	}

	private sealed class TestResultHandler
	{
		public string ResultToReturn { get; set; } = string.Empty;

		public Task<string> HandleAsync(TestMessage message, CancellationToken ct)
		{
			return Task.FromResult(ResultToReturn);
		}
	}

	private sealed class DynamicVoidHandler
	{
		public bool WasInvoked { get; private set; }

		public Task HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			WasInvoked = true;
			return Task.CompletedTask;
		}
	}

	private sealed class DynamicResultHandler
	{
		public string ResultToReturn { get; set; } = string.Empty;

		public Task<string> HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			return Task.FromResult(ResultToReturn);
		}
	}

	private sealed class DynamicIntResultHandler
	{
		public int ResultToReturn { get; set; }

		public Task<int> HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			return Task.FromResult(ResultToReturn);
		}
	}

	private sealed class CancellationAwareHandler
	{
		public CancellationToken ReceivedToken { get; private set; }

		public Task HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			ReceivedToken = ct;
			return Task.CompletedTask;
		}
	}

	private sealed class CachedHandler
	{
		public Task HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class ThreadSafeHandler
	{
		public Task HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingHandler
	{
		public InvalidOperationException ExceptionToThrow { get; set; } = new("Default error");

		public Task HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			throw ExceptionToThrow;
		}
	}

	private sealed class InvalidHandler
	{
		// No HandleAsync method - should cause error
		public void DoSomethingElse()
		{
		}
	}

	private sealed class WrongParameterCountHandler
	{
		// Wrong parameter count - should cause error
		public Task HandleAsync(IDispatchMessage message)
		{
			return Task.CompletedTask;
		}
	}

	private sealed class DelayedHandler
	{
		public int DelayMs { get; set; }
		public bool WasInvoked { get; private set; }

		public async Task HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			await Task.Delay(DelayMs, ct);
			WasInvoked = true;
		}
	}

	private sealed class DelayedResultHandler
	{
		public int DelayMs { get; set; }
		public string ResultToReturn { get; set; } = string.Empty;

		public async Task<string> HandleAsync(IDispatchMessage message, CancellationToken ct)
		{
			await Task.Delay(DelayMs, ct);
			return ResultToReturn;
		}
	}

	#endregion
}
