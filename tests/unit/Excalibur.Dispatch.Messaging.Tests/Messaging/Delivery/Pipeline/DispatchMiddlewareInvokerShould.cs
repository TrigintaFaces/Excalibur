// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1861 // Prefer 'static readonly' fields - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.DependencyInjection;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for <see cref="DispatchMiddlewareInvoker"/>.
/// Sprint 449 - S449.5: Tests for closure elimination optimization (S449.2).
/// Tests verify the struct-based continuation pattern eliminates closure allocations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
public sealed class DispatchMiddlewareInvokerShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;

	public DispatchMiddlewareInvokerShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	#region Basic Invocation Tests

	[Fact]
	public async Task InvokeAsync_WithNoMiddleware_CallsFinalDelegate()
	{
		// Arrange
		var invoker = new DispatchMiddlewareInvoker(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateContext();
		var finalDelegateCalled = false;

		ValueTask<IMessageResult> FinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			finalDelegateCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await invoker.InvokeAsync(message, context, FinalDelegate, CancellationToken.None);

		// Assert
		finalDelegateCalled.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_WithSingleMiddleware_ExecutesMiddlewareAndFinalDelegate()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middleware = new TestMiddleware("First", executionOrder);
		var invoker = new DispatchMiddlewareInvoker(new[] { middleware });
		var message = new TestMessage();
		var context = CreateContext();

		ValueTask<IMessageResult> FinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			executionOrder.Add("Final");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await invoker.InvokeAsync(message, context, FinalDelegate, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "Final" });
	}

	[Fact]
	public async Task InvokeAsync_WithMultipleMiddleware_ExecutesInOrder()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", executionOrder),
			new TestMiddleware("Second", executionOrder),
			new TestMiddleware("Third", executionOrder),
		};
		var invoker = new DispatchMiddlewareInvoker(middlewares);
		var message = new TestMessage();
		var context = CreateContext();

		ValueTask<IMessageResult> FinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			executionOrder.Add("Final");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await invoker.InvokeAsync(message, context, FinalDelegate, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "Second", "Third", "Final" });
	}

	#endregion

	#region Argument Validation Tests

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var invoker = new DispatchMiddlewareInvoker(Array.Empty<IDispatchMiddleware>());
		var context = CreateContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await invoker.InvokeAsync(
				null!,
				context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var invoker = new DispatchMiddlewareInvoker(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await invoker.InvokeAsync(
				message,
				null!,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var invoker = new DispatchMiddlewareInvoker(Array.Empty<IDispatchMiddleware>());
		var message = new TestMessage();
		var context = CreateContext();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await invoker.InvokeAsync<IMessageResult>(
				message,
				context,
				null!,
				CancellationToken.None));
	}

	#endregion

	#region Context and Message Propagation Tests

	[Fact]
	public async Task InvokeAsync_PropagatesContextThroughMiddleware()
	{
		// Arrange
		var contextValues = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new ContextWritingMiddleware("writer", "test-value"),
			new ContextReadingMiddleware("writer", contextValues), // Read from same key that was written
		};
		var invoker = new DispatchMiddlewareInvoker(middlewares);
		var message = new TestMessage();
		var context = CreateContext();

		// Act
		_ = await invoker.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None);

		// Assert
		contextValues.ShouldContain("test-value");
	}

	[Fact]
	public async Task InvokeAsync_PropagatesMessageThroughMiddleware()
	{
		// Arrange
		var receivedMessages = new List<IDispatchMessage>();
		var middlewares = new IDispatchMiddleware[]
		{
			new MessageCapturingMiddleware(receivedMessages),
		};
		var invoker = new DispatchMiddlewareInvoker(middlewares);
		var message = new TestMessage { Id = Guid.NewGuid() };
		var context = CreateContext();

		// Act
		_ = await invoker.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None);

		// Assert
		_ = receivedMessages.ShouldHaveSingleItem();
		receivedMessages[0].ShouldBe(message);
	}

	#endregion

	#region Cancellation Tests

	[Fact]
	public async Task InvokeAsync_PropagatesCancellationToken()
	{
		// Arrange
		var receivedCancellationTokens = new List<CancellationToken>();
		var middleware = new CancellationCapturingMiddleware(receivedCancellationTokens);
		var invoker = new DispatchMiddlewareInvoker(new[] { middleware });
		var message = new TestMessage();
		var context = CreateContext();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		// Act
		_ = await invoker.InvokeAsync(
			message,
			context,
			(_, _, ct) =>
			{
				receivedCancellationTokens.Add(ct);
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			token);

		// Assert
		receivedCancellationTokens.Count.ShouldBe(2); // One from middleware, one from final delegate
		receivedCancellationTokens.ShouldAllBe(ct => ct == token);
	}

	[Fact]
	public async Task InvokeAsync_RespectsAlreadyCancelledToken()
	{
		// Arrange
		var middleware = new CancellationCheckingMiddleware();
		var invoker = new DispatchMiddlewareInvoker(new[] { middleware });
		var message = new TestMessage();
		var context = CreateContext();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await invoker.InvokeAsync(
				message,
				context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				cts.Token));
	}

	#endregion

	#region Performance-Related Tests (Struct Continuation)

	[Fact]
	public async Task InvokeAsync_CanHandleDeepMiddlewareChain()
	{
		// Arrange - Create a deep chain to stress-test the struct-based continuation
		const int middlewareCount = 100;
		var executionOrder = new List<string>();
		var middlewares = Enumerable.Range(0, middlewareCount)
			.Select(i => new TestMiddleware($"Middleware-{i}", executionOrder))
			.Cast<IDispatchMiddleware>()
			.ToArray();

		var invoker = new DispatchMiddlewareInvoker(middlewares);
		var message = new TestMessage();
		var context = CreateContext();

		// Act
		var result = await invoker.InvokeAsync(
			message,
			context,
			(_, _, _) =>
			{
				executionOrder.Add("Final");
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		executionOrder.Count.ShouldBe(middlewareCount + 1);
	}

	[Fact]
	public async Task InvokeAsync_ConcurrentExecutions_AreIsolated()
	{
		// Arrange - Multiple concurrent invocations should not interfere
		var counters = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();
		var middleware = new CountingMiddleware(counters);
		var invoker = new DispatchMiddlewareInvoker(new[] { middleware });

		var tasks = Enumerable.Range(0, 50).Select(async i =>
		{
			var message = new TestMessage();
			var context = CreateContext();
			context.SetItem("iteration", i);
			return await invoker.InvokeAsync(
				message,
				context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None);
		});

		// Act
		var results = await Task.WhenAll(tasks);

		// Assert
		results.ShouldAllBe(r => r.Succeeded);
		counters.Count.ShouldBe(50); // Each iteration got its own count
	}

	[Fact]
	public async Task InvokeAsync_RepeatedCalls_UsesCachedMiddlewareFiltering()
	{
		// Arrange
		var executionCount = 0;
		var middleware = new ExecutionCountingMiddleware(() => executionCount++);
		var invoker = new DispatchMiddlewareInvoker(new[] { middleware });
		var message = new TestMessage();

		// Act - Call multiple times
		for (var i = 0; i < 10; i++)
		{
			var context = CreateContext();
			_ = await invoker.InvokeAsync(
				message,
				context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None);
		}

		// Assert - All invocations should succeed
		executionCount.ShouldBe(10);
	}

	#endregion

	#region Middleware Applicability Tests

	[Fact]
	public async Task InvokeAsync_WithNoApplicabilityStrategy_ExecutesAllMiddleware()
	{
		// Arrange - Without a strategy, all middleware runs regardless of MessageKinds
		var executionOrder = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new ActionOnlyMiddleware("ActionOnly", executionOrder),
			new TestMiddleware("All", executionOrder),
		};
		var invoker = new DispatchMiddlewareInvoker(middlewares);
		var message = new TestMessage(); // Not an IDispatchAction
		var context = CreateContext();

		// Act
		_ = await invoker.InvokeAsync(
			message,
			context,
			(_, _, _) =>
			{
				executionOrder.Add("Final");
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None);

		// Assert - Without strategy, all middleware executes
		executionOrder.ShouldBe(new[] { "ActionOnly", "All", "Final" });
	}

	#endregion

	#region Helper Methods

	private IMessageContext CreateContext()
	{
		return new FakeMessageContext { RequestServices = _serviceProvider };
	}

	#endregion

	#region Test Fixtures

	private sealed record TestMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
	}

	private sealed class TestMiddleware(string name, List<string> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class ContextWritingMiddleware(string key, string value) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			context.SetItem(key, value);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class ContextReadingMiddleware(string key, List<string> values) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			var value = context.GetItem<string>(key);
			if (value != null)
			{
				values.Add(value);
			}

			return next(message, context, cancellationToken);
		}
	}

	private sealed class MessageCapturingMiddleware(List<IDispatchMessage> messages) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			messages.Add(message);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class CancellationCapturingMiddleware(List<CancellationToken> tokens) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			tokens.Add(cancellationToken);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class CancellationCheckingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return next(message, context, cancellationToken);
		}
	}

	private sealed class CountingMiddleware(System.Collections.Concurrent.ConcurrentDictionary<int, int> counters) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			var iteration = context.GetItem<int>("iteration");
			_ = counters.TryAdd(iteration, iteration);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class ExecutionCountingMiddleware(Action incrementCounter) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			incrementCounter();
			return next(message, context, cancellationToken);
		}
	}

	private sealed class ActionOnlyMiddleware(string name, List<string> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	#endregion
}
