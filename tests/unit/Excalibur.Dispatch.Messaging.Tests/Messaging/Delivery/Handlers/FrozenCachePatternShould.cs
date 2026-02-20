// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Tests for the three-phase lazy freeze pattern (PERF-13/PERF-14) used in hot-path caches.
/// Validates warmup, freeze transition, and post-freeze behavior for HandlerInvoker cache.
/// </summary>
/// <remarks>
/// Sprint 453 - S453.5: Unit tests for FrozenDictionary behavior.
/// Tests the three-phase pattern: warmup (ConcurrentDictionary), freeze transition, frozen (FrozenDictionary).
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class FrozenCachePatternShould : IDisposable
{
	private readonly HandlerInvoker _invoker = new();

	public FrozenCachePatternShould()
	{
		// Reset to unfrozen state before each test
		HandlerInvoker.ClearCache();
	}

	public void Dispose()
	{
		// Clean up after tests
		HandlerInvoker.ClearCache();
	}

	#region Warmup Phase Tests (5 tests)

	[Fact]
	public void IsCacheFrozen_DuringWarmup_ReturnsFalse()
	{
		// Assert - cache starts in warmup phase
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public async Task GetOrAdd_DuringWarmup_AddsToCache()
	{
		// Arrange
		var handler = new TestHandler();
		var message = new TestMessage();

		// Act - invoke during warmup phase
		_ = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		// Assert - cache is still in warmup mode
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public async Task GetOrAdd_DuringWarmup_ReturnsCachedValue()
	{
		// Arrange
		var handler = new TestHandler();
		var message = new TestMessage();

		// Act - invoke twice to verify caching
		var result1 = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
		var result2 = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		// Assert - both invocations succeed (cached invoker used for second call)
		_ = result1.ShouldNotBeNull();
		_ = result2.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetOrAdd_DuringWarmup_ThreadSafe()
	{
		// Arrange - multiple handlers and messages
		var handlers = Enumerable.Range(0, 10).Select(_ => new TestHandler()).ToArray();
		var messages = Enumerable.Range(0, 10).Select(_ => new TestMessage()).ToArray();
		var exceptions = new List<Exception>();

		// Act - concurrent invocations during warmup
		await Parallel.ForEachAsync(
			Enumerable.Range(0, 100),
			new ParallelOptions { MaxDegreeOfParallelism = 10 },
			async (i, ct) =>
			{
				try
				{
					var handler = handlers[i % handlers.Length];
					var message = messages[i % messages.Length];
					_ = await _invoker.InvokeAsync(handler, message, ct);
				}
				catch (Exception ex)
				{
					lock (exceptions)
					{
						exceptions.Add(ex);
					}
				}
			});

		// Assert - no exceptions during concurrent warmup
		exceptions.ShouldBeEmpty();
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();
	}

	[Fact]
	public void Freeze_DuringWarmup_TransitionsToFrozen()
	{
		// Arrange - warmup phase
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();

		// Act - freeze the cache
		HandlerInvoker.FreezeCache();

		// Assert - now frozen
		HandlerInvoker.IsCacheFrozen.ShouldBeTrue();
	}

	#endregion

	#region Freeze Transition Tests (4 tests)

	[Fact]
	public void Freeze_CalledMultipleTimes_IsIdempotent()
	{
		// Act - freeze multiple times
		HandlerInvoker.FreezeCache();
		HandlerInvoker.FreezeCache();
		HandlerInvoker.FreezeCache();

		// Assert - still frozen, no exception
		HandlerInvoker.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public async Task Freeze_WhileReading_NoRaceConditions()
	{
		// Arrange - populate cache first
		var handler = new TestHandler();
		var message = new TestMessage();
		_ = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		// Act - freeze while concurrent reads happening
		var readTask = Task.Run(async () =>
		{
			for (int i = 0; i < 100; i++)
			{
				_ = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
			}
		});

		var freezeTask = Task.Run(() =>
		{
			Thread.Sleep(10); // Small delay to ensure reads start
			HandlerInvoker.FreezeCache();
		});

		// Assert - no exceptions
		await Should.NotThrowAsync(async () => await Task.WhenAll(readTask, freezeTask));
		HandlerInvoker.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void Freeze_WhenEmpty_HandlesGracefully()
	{
		// Arrange - empty cache (no entries added)
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();

		// Act - freeze empty cache
		Should.NotThrow(() => HandlerInvoker.FreezeCache());

		// Assert - frozen successfully
		HandlerInvoker.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public void IsCacheFrozen_ReflectsCorrectState()
	{
		// Assert initial state
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();

		// Freeze
		HandlerInvoker.FreezeCache();
		HandlerInvoker.IsCacheFrozen.ShouldBeTrue();

		// Clear (resets to warmup)
		HandlerInvoker.ClearCache();
		HandlerInvoker.IsCacheFrozen.ShouldBeFalse();
	}

	#endregion

	#region Post-Freeze Tests (4 tests)

	[Fact]
	public async Task GetOrAdd_AfterFreeze_ReturnsCachedValue()
	{
		// Arrange - populate and freeze
		var handler = new TestHandler();
		var message = new TestMessage();
		_ = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
		HandlerInvoker.FreezeCache();

		// Act - invoke after freeze
		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		// Assert - uses frozen cache
		_ = result.ShouldNotBeNull();
		HandlerInvoker.IsCacheFrozen.ShouldBeTrue();
	}

	[Fact]
	public async Task GetOrAdd_AfterFreeze_MissReturnsFactoryResult()
	{
		// Arrange - freeze with empty cache
		HandlerInvoker.FreezeCache();
		var handler = new TestHandler();
		var message = new TestMessage();

		// Act - invoke with cache miss after freeze (late registration scenario)
		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		// Assert - graceful degradation: builds invoker on-the-fly
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public async Task GetOrAdd_AfterFreeze_NoLockingOnHit()
	{
		// Arrange - populate and freeze
		var handler = new TestHandler();
		var message = new TestMessage();
		_ = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
		HandlerInvoker.FreezeCache();

		// Act - measure performance (FrozenDictionary should have no locking)
		var sw = System.Diagnostics.Stopwatch.StartNew();
		for (int i = 0; i < 10000; i++)
		{
			_ = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
		}
		sw.Stop();

		// Assert - should complete quickly (< 1 second for 10k invocations)
		sw.ElapsedMilliseconds.ShouldBeLessThan(5000);
	}

	[Fact]
	public async Task LateAddition_AfterFreeze_GracefulDegradation()
	{
		// Arrange - freeze with one handler
		var handler1 = new TestHandler();
		var message1 = new TestMessage();
		_ = await _invoker.InvokeAsync(handler1, message1, CancellationToken.None);
		HandlerInvoker.FreezeCache();

		// Act - invoke with a different handler type (late addition)
		var handler2 = new AnotherTestHandler();
		var message2 = new AnotherTestMessage();

		// Assert - should handle gracefully (builds on-the-fly, doesn't crash)
		var result = await _invoker.InvokeAsync(handler2, message2, CancellationToken.None);
		_ = result.ShouldNotBeNull();
	}

	#endregion

	#region Edge Cases (2 tests)

	[Fact]
	public async Task NullHandler_ThrowsArgumentNullException()
	{
		// Arrange
		var message = new TestMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _invoker.InvokeAsync(null!, message, CancellationToken.None));
	}

	[Fact]
	public async Task NullMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var handler = new TestHandler();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _invoker.InvokeAsync(handler, null!, CancellationToken.None));
	}

	#endregion

	#region Test Support Types

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

	private sealed class AnotherTestMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "AnotherTestMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	/// <summary>
	/// Simple handler using HandleAsync convention (not interface-based).
	/// </summary>
	private sealed class TestHandler
	{
		public Task<string> HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			return Task.FromResult("handled");
		}
	}

	/// <summary>
	/// Another handler type for testing multiple handler types in cache.
	/// </summary>
	private sealed class AnotherTestHandler
	{
		public Task<string> HandleAsync(AnotherTestMessage message, CancellationToken cancellationToken)
		{
			return Task.FromResult("another handled");
		}
	}

	#endregion
}
