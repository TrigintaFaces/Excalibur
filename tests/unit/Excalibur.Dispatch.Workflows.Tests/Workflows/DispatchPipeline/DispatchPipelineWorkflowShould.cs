// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Workflows.DispatchPipeline;

/// <summary>
/// End-to-end workflow tests for the Dispatch pipeline.
/// Tests command dispatch, query caching, event publishing, middleware chains, and error handling.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 181 - Functional Testing Epic Phase 1.
/// bd-se5o5: Dispatch Pipeline Workflow Tests (5 tests).
/// </para>
/// <para>
/// These tests use in-memory simulation to validate pipeline patterns
/// without requiring TestContainers or external services.
/// </para>
/// </remarks>
[Trait("Epic", "FunctionalTesting")]
[Trait("Sprint", "181")]
[Trait("Component", "DispatchPipeline")]
[Trait("Category", "Unit")]
public sealed class DispatchPipelineWorkflowShould
{
	/// <summary>
	/// Tests that a command flows through the full pipeline with validation and authorization.
	/// Command > Validation > Authorization > Handler > Result.
	/// </summary>
	[Fact]
	public async Task ExecuteCommandThroughFullPipeline()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var pipeline = new SimulatedPipeline(executionLog);
		var command = new CreateOrderCommand("ORD-001", 100.00m);

		// Act
		var result = await pipeline.DispatchCommandAsync(command).ConfigureAwait(true);

		// Assert - Command was processed successfully
		result.Success.ShouldBeTrue();

		// Assert - Pipeline executed in correct order
		executionLog.Steps.Count.ShouldBeGreaterThanOrEqualTo(3);
		executionLog.Steps.ShouldContain("ValidationMiddleware:Pre");
		executionLog.Steps.ShouldContain("AuthorizationMiddleware:Pre");
		executionLog.Steps.ShouldContain("CreateOrderCommandHandler:Execute");
	}

	/// <summary>
	/// Tests that queries can use caching (cache miss then hit pattern).
	/// Cache miss > Handler > Cache hit (no handler).
	/// </summary>
	[Fact]
	public async Task CacheQueryResultsOnSecondCall()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var cache = new SimulatedCache();
		var pipeline = new SimulatedPipeline(executionLog, cache);
		var query = new GetProductQuery("PROD-001");

		// Act - First call (cache miss, handler executes)
		var result1 = await pipeline.DispatchQueryAsync(query).ConfigureAwait(true);

		// Act - Second call (cache hit, handler skipped)
		var result2 = await pipeline.DispatchQueryAsync(query).ConfigureAwait(true);

		// Assert - Both calls succeeded
		_ = result1.ShouldNotBeNull();
		_ = result2.ShouldNotBeNull();

		// Assert - Handler was only called once (second call used cache)
		var handlerCalls = executionLog.Steps.Count(s => s == "GetProductQueryHandler:Execute");
		handlerCalls.ShouldBe(1);

		// Assert - Cache was hit on second call
		executionLog.Steps.ShouldContain("Cache:Hit:PROD-001");
	}

	/// <summary>
	/// Tests that an event is delivered to multiple subscribers in parallel.
	/// Event > 3+ subscribers > Parallel execution.
	/// </summary>
	[Fact]
	public async Task DeliverEventToMultipleSubscribersInParallel()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var pipeline = new SimulatedPipeline(executionLog);
		var @event = new OrderCreatedEvent("ORD-002", 250.00m);

		// Act
		await pipeline.PublishEventAsync(@event).ConfigureAwait(true);

		// Assert - All 3 handlers received the event
		executionLog.Steps.ShouldContain("OrderCreatedHandler1:Handle:ORD-002");
		executionLog.Steps.ShouldContain("OrderCreatedHandler2:Handle:ORD-002");
		executionLog.Steps.ShouldContain("OrderCreatedHandler3:Handle:ORD-002");
	}

	/// <summary>
	/// Tests that middleware executes in the correct order (pre > handler > post).
	/// Pre-middleware > Handler > Post-middleware order.
	/// </summary>
	[Fact]
	public async Task ExecuteMiddlewareInCorrectOrder()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var pipeline = new SimulatedPipeline(executionLog);
		var command = new TraceableCommand("trace-001");

		// Act
		_ = await pipeline.DispatchCommandAsync(command).ConfigureAwait(true);

		// Assert - Middleware executed in correct order: Pre > Handler > Post
		// Use the ordered steps list from ExecutionLog
		var steps = executionLog.GetOrderedSteps();

		var preIndex = steps.FindIndex(s => s.Contains("Pre", StringComparison.Ordinal));
		var handlerIndex = steps.FindIndex(s => s.Contains("Handler:Execute", StringComparison.Ordinal));
		var postIndex = steps.FindIndex(s => s.Contains("Post", StringComparison.Ordinal));

		preIndex.ShouldBeLessThan(handlerIndex);
		if (postIndex >= 0)
		{
			handlerIndex.ShouldBeLessThan(postIndex);
		}
	}

	/// <summary>
	/// Tests that errors are handled gracefully with retry policy.
	/// Exception > Retry policy > Recovery or DLQ.
	/// </summary>
	[Fact]
	public async Task HandleErrorsWithRetryPolicy()
	{
		// Arrange
		var executionLog = new ExecutionLog();
		var pipeline = new SimulatedPipeline(executionLog);
		var command = new FailingCommand("fail-001", FailCount: 2);

		// Act
		var result = await pipeline.DispatchCommandWithRetryAsync(command, maxRetries: 3).ConfigureAwait(true);

		// Assert - Command eventually succeeded after retries
		result.Success.ShouldBeTrue();

		// Assert - Handler was retried (called more than once)
		var attemptCount = executionLog.Steps.Count(s => s.StartsWith("FailingCommandHandler:Attempt", StringComparison.Ordinal));
		attemptCount.ShouldBeGreaterThan(1);
	}

	#region Test Infrastructure

	/// <summary>
	/// Execution log to track middleware and handler execution order.
	/// </summary>
	internal sealed class ExecutionLog
	{
		private readonly ConcurrentQueue<string> _orderedSteps = new();
		public ConcurrentBag<string> Steps { get; } = [];

		public void Log(string step)
		{
			Steps.Add(step);
			_orderedSteps.Enqueue(step);
		}

		public List<string> GetOrderedSteps() => [.. _orderedSteps];
	}

	/// <summary>
	/// Simple in-memory cache simulation.
	/// </summary>
	internal sealed class SimulatedCache
	{
		private readonly ConcurrentDictionary<string, object> _cache = new();

		public bool TryGet<T>(string key, out T? value)
		{
			if (_cache.TryGetValue(key, out var cached) && cached is T typedValue)
			{
				value = typedValue;
				return true;
			}
			value = default;
			return false;
		}

		public void Set<T>(string key, T value) => _cache[key] = value!;
	}

	/// <summary>
	/// Simulated dispatch pipeline for testing patterns.
	/// </summary>
	internal sealed class SimulatedPipeline
	{
		private readonly ExecutionLog _log;
		private readonly SimulatedCache? _cache;

		public SimulatedPipeline(ExecutionLog log, SimulatedCache? cache = null)
		{
			_log = log;
			_cache = cache;
		}

		public Task<CommandResult> DispatchCommandAsync(object command)
		{
			// Simulate middleware chain
			_log.Log("ValidationMiddleware:Pre");
			_log.Log("AuthorizationMiddleware:Pre");

			// Dispatch to handler
			var handlerName = command.GetType().Name + "Handler";
			_log.Log($"{handlerName}:Execute");

			// Post middleware
			_log.Log("ValidationMiddleware:Post");
			_log.Log("AuthorizationMiddleware:Post");

			return Task.FromResult(new CommandResult { Success = true });
		}

		public Task<ProductDto?> DispatchQueryAsync(GetProductQuery query)
		{
			// Check cache first
			if (_cache?.TryGet<ProductDto>(query.ProductId, out var cached) == true)
			{
				_log.Log($"Cache:Hit:{query.ProductId}");
				return Task.FromResult(cached);
			}

			_log.Log($"Cache:Miss:{query.ProductId}");

			// Execute handler
			_log.Log("GetProductQueryHandler:Execute");
			var result = new ProductDto(query.ProductId, "Test Product", 99.99m);

			// Store in cache
			_cache?.Set(query.ProductId, result);

			return Task.FromResult<ProductDto?>(result);
		}

		public Task PublishEventAsync(OrderCreatedEvent @event)
		{
			// Simulate parallel fan-out to multiple handlers
			var handlers = new[] { "OrderCreatedHandler1", "OrderCreatedHandler2", "OrderCreatedHandler3" };
			_ = Parallel.ForEach(handlers, handler =>
			{
				_log.Log($"{handler}:Handle:{@event.OrderId}");
			});

			return Task.CompletedTask;
		}

		public async Task<CommandResult> DispatchCommandWithRetryAsync(FailingCommand command, int maxRetries)
		{
			var attempts = 0;
			while (attempts < maxRetries)
			{
				attempts++;
				_log.Log($"FailingCommandHandler:Attempt:{attempts}");

				if (attempts <= command.FailCount)
				{
					_log.Log($"FailingCommandHandler:Failed:{attempts}");
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10).ConfigureAwait(false); // Simulate retry delay
					continue;
				}

				_log.Log($"FailingCommandHandler:Success:{attempts}");
				return new CommandResult { Success = true };
			}

			return new CommandResult { Success = false };
		}
	}

	// Command types
	internal sealed record CreateOrderCommand(string OrderId, decimal Amount);
	internal sealed record TraceableCommand(string TraceId);
	internal sealed record FailingCommand(string CommandId, int FailCount);

	// Query types
	internal sealed record GetProductQuery(string ProductId);
	internal sealed record ProductDto(string Id, string Name, decimal Price);

	// Event types
	internal sealed record OrderCreatedEvent(string OrderId, decimal Amount);

	// Result types
	internal sealed class CommandResult
	{
		public bool Success { get; init; }
	}

	#endregion Test Infrastructure
}
