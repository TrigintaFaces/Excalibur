// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1034 // Nested types should not be visible - acceptable in test classes
#pragma warning disable CA1063 // Implement IDisposable correctly - simplified for test classes

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.EndToEnd;

/// <summary>
/// End-to-end integration tests that dispatch messages through the complete
/// Excalibur framework pipeline using the real IDispatcher.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EndToEnd")]
public sealed class DispatcherEndToEndIntegrationShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;
	private readonly IDispatcher _dispatcher;
	private readonly IMessageContextFactory _contextFactory;

	public DispatcherEndToEndIntegrationShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register test handlers BEFORE AddDispatch - the handler registry is built during AddDispatch
		// and captures handlers registered at that moment
		// Must register BOTH the interface AND the concrete type (activator resolves concrete type)
		_ = services.AddTransient<TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		_ = services.AddTransient<TestQueryHandler>();
		_ = services.AddTransient<IActionHandler<TestQuery, TestQueryResult>, TestQueryHandler>();

		_ = services.AddTransient<SlowCommandHandler>();
		_ = services.AddTransient<IActionHandler<SlowCommand>, SlowCommandHandler>();

		_ = services.AddTransient<CountingCommandHandler>();
		_ = services.AddTransient<IActionHandler<CountingCommand>, CountingCommandHandler>();

		// Wire up the full Excalibur framework (without assembly scanning to avoid open generic handlers)
		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	[Fact]
	public async Task DispatchSimpleCommandThroughFullPipeline()
	{
		// Arrange
		var command = new TestCommand { Id = Guid.NewGuid(), Data = "TestData" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
		TestCommandHandler.ProcessedCommands.ShouldContain(command.Id);
	}

	[Fact]
	public async Task DispatchQueryAndReturnResultThroughFullPipeline()
	{
		// Arrange
		var query = new TestQuery { UserId = Guid.NewGuid() };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync<TestQuery, TestQueryResult>(query, context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
		_ = result.ReturnValue.ShouldNotBeNull();
		result.ReturnValue.UserId.ShouldBe(query.UserId);
		result.ReturnValue.UserName.ShouldBe("TestUser");
	}

	[Fact]
	public async Task DispatchSlowCommandSuccessfully()
	{
		// Arrange - Use a short delay to verify the handler actually runs
		var command = new SlowCommand { DelayMs = 10 };
		var context = _contextFactory.CreateContext();

		// Verify handler is registered
		var registry = _serviceProvider.GetRequiredService<Dispatch.Delivery.Handlers.IHandlerRegistry>();
		var hasHandler = registry.TryGetHandler(typeof(SlowCommand), out var entry);
		hasHandler.ShouldBeTrue("SlowCommand handler should be registered");
		entry.HandlerType.Name.ShouldBe(nameof(SlowCommandHandler));

		// Act
		var sw = Stopwatch.StartNew();
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);
		sw.Stop();

		// Assert - Handler should complete successfully
		result.Succeeded.ShouldBeTrue($"Expected success, got: {result.ErrorMessage}");
		// The dispatch should have taken at least the delay time (handler was actually invoked)
		sw.Elapsed.TotalMilliseconds.ShouldBeGreaterThan(5, "Handler should have executed the delay");
	}

	[Fact]
	public async Task DispatchMultipleCommandsConcurrently()
	{
		// Arrange
		var commandCount = 100;
		var commands = Enumerable.Range(0, commandCount)
			.Select(i => new TestCommand { Id = Guid.NewGuid(), Data = $"Data{i}" })
			.ToList();

		// Act
		var tasks = commands.Select(cmd =>
		{
			var context = _contextFactory.CreateContext();
			return _dispatcher.DispatchAsync(cmd, context, CancellationToken.None);
		});

		var results = await Task.WhenAll(tasks);

		// Assert
		results.Length.ShouldBe(commandCount);
		results.ShouldAllBe(r => r.Succeeded);

		// Verify all commands were processed
		foreach (var cmd in commands)
		{
			TestCommandHandler.ProcessedCommands.ShouldContain(cmd.Id);
		}
	}

	[Fact]
	public async Task MeasureThroughput_ShouldExceed10KMessagesPerSecond()
	{
		// Arrange
		var messageCount = 10_000;
		CountingCommandHandler.ResetCounter();

		var commands = Enumerable.Range(0, messageCount)
			.Select(_ => new CountingCommand())
			.ToList();

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			var ctx = _contextFactory.CreateContext();
			_ = await _dispatcher.DispatchAsync(new CountingCommand(), ctx, CancellationToken.None);
		}

		CountingCommandHandler.ResetCounter();

		// Act
		var stopwatch = Stopwatch.StartNew();

		var tasks = commands.Select(cmd =>
		{
			var context = _contextFactory.CreateContext();
			return _dispatcher.DispatchAsync(cmd, context, CancellationToken.None);
		});

		_ = await Task.WhenAll(tasks);

		stopwatch.Stop();

		// Calculate throughput
		var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
		var throughput = messageCount / elapsedSeconds;

		// Assert
		CountingCommandHandler.Counter.ShouldBe(messageCount);
		throughput.ShouldBeGreaterThan(10_000, $"Throughput was {throughput:N0} msg/sec, expected > 10K msg/sec");
	}

	[Fact]
	public async Task MeasureLatency_P50ShouldBeUnder1Ms()
	{
		// Arrange
		var messageCount = 1000;
		var latencies = new List<double>(messageCount);
		var command = new TestCommand { Id = Guid.NewGuid(), Data = "LatencyTest" };

		// Warmup
		for (var i = 0; i < 100; i++)
		{
			var ctx = _contextFactory.CreateContext();
			_ = await _dispatcher.DispatchAsync(command, ctx, CancellationToken.None);
		}

		// Act - Measure individual dispatch latencies
		for (var i = 0; i < messageCount; i++)
		{
			var context = _contextFactory.CreateContext();
			var sw = Stopwatch.StartNew();

			_ = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

			sw.Stop();
			latencies.Add(sw.Elapsed.TotalMilliseconds);
		}

		// Calculate percentiles
		latencies.Sort();
		var p50 = latencies[messageCount / 2];
		var p95 = latencies[(int)(messageCount * 0.95)];
		var p99 = latencies[(int)(messageCount * 0.99)];

		// Assert
		p50.ShouldBeLessThan(1.0, $"P50 latency was {p50:F3}ms, expected < 1ms");
		p95.ShouldBeLessThan(5.0, $"P95 latency was {p95:F3}ms, expected < 5ms");
		p99.ShouldBeLessThan(10.0, $"P99 latency was {p99:F3}ms, expected < 10ms");
	}

	[Fact]
	public async Task PropagateContextThroughPipeline()
	{
		// Arrange
		var command = new TestCommand { Id = Guid.NewGuid(), Data = "ContextTest" };
		var context = _contextFactory.CreateContext();
		context.CorrelationId = "test-correlation-id";
		context.TenantId = "test-tenant";
		context.UserId = "test-user";

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		_ = TestCommandHandler.LastContext.ShouldNotBeNull();
		TestCommandHandler.LastContext.CorrelationId.ShouldBe("test-correlation-id");
		TestCommandHandler.LastContext.TenantId.ShouldBe("test-tenant");
		TestCommandHandler.LastContext.UserId.ShouldBe("test-user");
	}

	[Fact]
	public async Task HandleFailureGracefully()
	{
		// Arrange
		var command = new TestCommand { Id = Guid.NewGuid(), Data = "FAIL" };
		var context = _contextFactory.CreateContext();

		// Act
		var result = await _dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldNotBeNullOrEmpty();
	}

	// Test Messages
	public sealed record TestCommand : IDispatchAction
	{
		public Guid Id { get; init; }
		public string Data { get; init; } = string.Empty;
	}

	public sealed record TestQuery : IDispatchAction<TestQueryResult>
	{
		public Guid UserId { get; init; }
	}

	public sealed record TestQueryResult
	{
		public Guid UserId { get; init; }
		public string UserName { get; init; } = string.Empty;
	}

	public sealed record SlowCommand : IDispatchAction
	{
		public int DelayMs { get; init; }
	}

	public sealed record CountingCommand : IDispatchAction;

	// Test Handlers
	public sealed class TestCommandHandler : IActionHandler<TestCommand>
	{
		public static ConcurrentBag<Guid> ProcessedCommands { get; } = [];
		public static IMessageContext? LastContext { get; private set; }

		public Task HandleAsync(TestCommand action, CancellationToken cancellationToken)
		{
			if (action.Data == "FAIL")
			{
				throw new InvalidOperationException("Command failed as requested");
			}

			ProcessedCommands.Add(action.Id);
			LastContext = MessageContextHolder.Current;
			return Task.CompletedTask;
		}
	}

	public sealed class TestQueryHandler : IActionHandler<TestQuery, TestQueryResult>
	{
		public Task<TestQueryResult> HandleAsync(TestQuery action, CancellationToken cancellationToken)
		{
			return Task.FromResult(new TestQueryResult
			{
				UserId = action.UserId,
				UserName = "TestUser",
			});
		}
	}

	public sealed class SlowCommandHandler : IActionHandler<SlowCommand>
	{
		public async Task HandleAsync(SlowCommand action, CancellationToken cancellationToken)
		{
			await Task.Delay(action.DelayMs, cancellationToken);
		}
	}

	public sealed class CountingCommandHandler : IActionHandler<CountingCommand>
	{
		private static int _counter;

		public static int Counter => _counter;

		public static void ResetCounter() => Interlocked.Exchange(ref _counter, 0);

		public Task HandleAsync(CountingCommand action, CancellationToken cancellationToken)
		{
			_ = Interlocked.Increment(ref _counter);
			return Task.CompletedTask;
		}
	}
}
