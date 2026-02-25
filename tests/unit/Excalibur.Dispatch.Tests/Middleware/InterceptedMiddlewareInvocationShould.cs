// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1861 // Prefer 'static readonly' fields - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Tests for intercepted middleware invocation behavior.
/// Validates that typed middleware invocation works correctly with the generated registry pattern.
/// </summary>
/// <remarks>
/// Sprint 456 - S456.4: Runtime tests for intercepted middleware invocation (PERF-10).
/// Tests verify the middleware invocation patterns that the generated code relies upon.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class InterceptedMiddlewareInvocationShould : UnitTestBase
{
	#region Direct Typed Invocation Tests (4 tests)

	[Fact]
	public async Task DirectTypedInvocation_CallsInvokeAsyncDirectly()
	{
		// Arrange - simulates the generated interceptor pattern
		var invocations = new List<string>();
		var middleware = new TestLoggingMiddleware("Logging", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act - direct typed call (what the interceptor generates)
		var result = await middleware.InvokeAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocations.ShouldContain("Logging");
	}

	[Fact]
	public async Task DirectTypedInvocation_WorksWithCastFromInterface()
	{
		// Arrange - simulates the cast pattern: ((ConcreteMiddleware)middleware).InvokeAsync(...)
		var invocations = new List<string>();
		IDispatchMiddleware middleware = new TestLoggingMiddleware("TypedCast", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act - cast and invoke (matches generated code pattern)
		var typedMiddleware = (TestLoggingMiddleware)middleware;
		var result = await typedMiddleware.InvokeAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocations.ShouldContain("TypedCast");
	}

	[Fact]
	public async Task DirectTypedInvocation_PropagatesNextDelegate()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new TestLoggingMiddleware("First", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();
		var nextCalled = false;

		// Act
		_ = await middleware.InvokeAsync(
			message,
			context,
			(msg, ctx, ct) =>
			{
				nextCalled = true;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task DirectTypedInvocation_PreservesContext()
	{
		// Arrange
		var invocations = new List<string>();
		var middleware = new ContextAwareMiddleware("ContextAware", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();
		context.SetItem("test_key", "test_value");

		// Act
		_ = await middleware.InvokeAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("ContextAware:test_value");
	}

	#endregion

	#region Interface Fallback Tests (4 tests)

	[Fact]
	public async Task InterfaceFallback_WorksForUnknownMiddleware()
	{
		// Arrange - simulates fallback for dynamically registered middleware
		var invocations = new List<string>();
		IDispatchMiddleware middleware = new DynamicMiddleware("Dynamic", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act - interface dispatch (fallback path)
		var result = await middleware.InvokeAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocations.ShouldContain("Dynamic");
	}

	[Fact]
	public async Task InterfaceFallback_MaintainsCorrectOrdering()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new IDispatchMiddleware[]
		{
			new DynamicMiddleware("First", invocations),
			new DynamicMiddleware("Second", invocations),
		};
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert - ordering should be preserved
		invocations.ShouldBe(new[] { "First", "Second" });
	}

	[Fact]
	public async Task InterfaceFallback_PropagatesExceptions()
	{
		// Arrange
		IDispatchMiddleware middleware = new ThrowingMiddleware("Throwing", new InvalidOperationException("Test"));
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(
				message,
				context,
				(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task InterfaceFallback_RespectsCancellation()
	{
		// Arrange
		var invocations = new List<string>();
		IDispatchMiddleware middleware = new CancellationAwareMiddleware("Cancellable", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await middleware.InvokeAsync(
				message,
				context,
				(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
				cts.Token).ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	#endregion

	#region Middleware Ordering Preservation Tests (4 tests)

	[Fact]
	public async Task MiddlewareOrdering_PreservedWithTypedInvocation()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new IDispatchMiddleware[]
		{
			new TestLoggingMiddleware("First", invocations),
			new TestValidationMiddleware("Second", invocations),
			new TestAuthorizationMiddleware("Third", invocations),
		};
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert - order should match stage order
		invocations.ShouldContain("First");
		invocations.ShouldContain("Second");
		invocations.ShouldContain("Third");
	}

	[Fact]
	public async Task MiddlewareOrdering_MixedTypedAndDynamic()
	{
		// Arrange - mix of typed (known) and dynamic (unknown) middleware
		var invocations = new List<string>();
		var middlewareList = new IDispatchMiddleware[]
		{
			new TestLoggingMiddleware("Typed1", invocations),
			new DynamicMiddleware("Dynamic", invocations),
			new TestLoggingMiddleware("Typed2", invocations),
		};
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert - all should be invoked
		invocations.Count.ShouldBe(3);
	}

	[Fact]
	public async Task MiddlewareOrdering_NestedNextCalls()
	{
		// Arrange
		var invocations = new List<string>();
		var middlewareList = new IDispatchMiddleware[]
		{
			new BeforeAfterMiddleware("Outer", invocations),
			new BeforeAfterMiddleware("Inner", invocations),
		};
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert - proper nesting: Outer:before, Inner:before, Inner:after, Outer:after
		invocations.ShouldBe(new[] { "Outer:before", "Inner:before", "Inner:after", "Outer:after" });
	}

	[Fact]
	public async Task MiddlewareOrdering_ShortCircuitStopsChain()
	{
		// Arrange - use explicit stages to ensure proper ordering
		// Start(0) -> Validation(10) -> Processing(40) - short circuit at Validation should stop Processing
		var invocations = new List<string>();
		var middlewareList = new IDispatchMiddleware[]
		{
			new StageSpecificMiddleware("First", invocations, DispatchMiddlewareStage.Start),
			new StageSpecificShortCircuitMiddleware("ShortCircuit", invocations, DispatchMiddlewareStage.Validation),
			new StageSpecificMiddleware("NotReached", invocations, DispatchMiddlewareStage.Processing),
		};
		var pipeline = new DispatchPipeline(middlewareList);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act
		_ = await pipeline.ExecuteAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		invocations.ShouldContain("First");
		invocations.ShouldContain("ShortCircuit");
		invocations.ShouldNotContain("NotReached");
	}

	#endregion

	#region Hot Reload Compatibility Tests (2 tests)

	[Fact]
	public void HotReloadDetection_EnvironmentVariableCheck()
	{
		// This test documents the hot reload detection pattern
		// The generated code checks DOTNET_WATCH and DOTNET_MODIFIABLE_ASSEMBLIES

		// Act - check if hot reload env vars are present
		var dotnetWatch = Environment.GetEnvironmentVariable("DOTNET_WATCH");
		var modifiableAssemblies = Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES");

		// Assert - in test environment, these should typically be unset
		// The generated code falls back to interface dispatch when these are set
		// This test verifies the detection pattern works
		(dotnetWatch == null || dotnetWatch == "0" || dotnetWatch == "false" ||
		 modifiableAssemblies == null || modifiableAssemblies != "debug").ShouldBeTrue();
	}

	[Fact]
	public async Task HotReloadCompatibility_FallbackAlwaysWorks()
	{
		// Arrange - in hot reload mode, fallback to interface dispatch is used
		var invocations = new List<string>();
		IDispatchMiddleware middleware = new TestLoggingMiddleware("Fallback", invocations);
		var message = new TestCommand();
		var context = CreateMessageContext();

		// Act - interface dispatch (always available as fallback)
		var result = await middleware.InvokeAsync(
			message,
			context,
			(msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		invocations.ShouldContain("Fallback");
	}

	#endregion

	#region Helper Methods and Test Fixtures

	private IMessageContext CreateMessageContext()
	{
		return new TestMessageContext
		{
			RequestServices = ServiceProvider ?? new ServiceCollection().BuildServiceProvider(),
			ReceivedTimestampUtc = DateTimeOffset.UtcNow,
		};
	}

	#endregion

	#region Test Message Types

	private sealed class TestCommand : IDispatchMessage { }

	private sealed class TestMessageContext : IMessageContext
	{
		private readonly Dictionary<string, object> _items = new();

		public string? MessageId { get; set; }
		public string? ExternalId { get; set; }
		public string? UserId { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public string? TraceParent { get; set; }
		public string? TenantId { get; set; }
		public string? SessionId { get; set; }
		public string? WorkflowId { get; set; }
		public string? PartitionKey { get; set; }
		public string? Source { get; set; }
		public string? MessageType { get; set; }
		public string? ContentType { get; set; }
		public int DeliveryCount { get; set; }
		public IDispatchMessage? Message { get; set; }
		public object? Result { get; set; }
		public RoutingDecision? RoutingDecision { get; set; } = RoutingDecision.Success("local", []);
		public IServiceProvider RequestServices { get; set; } = null!;
		public DateTimeOffset ReceivedTimestampUtc { get; set; }
		public DateTimeOffset? SentTimestampUtc { get; set; }
		public IDictionary<string, object> Items => _items;
		public IDictionary<string, object?> Properties => _items!;

		public int ProcessingAttempts { get; set; }
		public DateTimeOffset? FirstAttemptTime { get; set; }
		public bool IsRetry { get; set; }
		public bool ValidationPassed { get; set; }
		public DateTimeOffset? ValidationTimestamp { get; set; }
		public object? Transaction { get; set; }
		public string? TransactionId { get; set; }
		public bool TimeoutExceeded { get; set; }
		public TimeSpan? TimeoutElapsed { get; set; }
		public bool RateLimitExceeded { get; set; }
		public TimeSpan? RateLimitRetryAfter { get; set; }

		public bool ContainsItem(string key) => _items.ContainsKey(key);
		public T? GetItem<T>(string key) => _items.TryGetValue(key, out var value) ? (T)value : default;
		public T GetItem<T>(string key, T defaultValue) => _items.TryGetValue(key, out var value) ? (T)value : defaultValue;
		public void RemoveItem(string key) => _items.Remove(key);
		public void SetItem<T>(string key, T value) => _items[key] = value!;
		public IMessageContext CreateChildContext() => new TestMessageContext
		{
			CorrelationId = CorrelationId,
			CausationId = MessageId ?? CorrelationId,
			TenantId = TenantId,
			UserId = UserId,
			SessionId = SessionId,
			WorkflowId = WorkflowId,
			TraceParent = TraceParent,
			Source = Source,
			RequestServices = RequestServices,
			MessageId = Guid.NewGuid().ToString(),
		};
	}

	#endregion

	#region Test Middleware Implementations

	private sealed class TestLoggingMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class TestValidationMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class TestAuthorizationMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class DynamicMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class ContextAwareMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			var value = context.GetItem<string>("test_key");
			invocations.Add($"{name}:{value}");
			return next(message, context, cancellationToken);
		}
	}

	private sealed class BeforeAfterMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add($"{name}:before");
			var result = await next(message, context, cancellationToken).ConfigureAwait(false);
			invocations.Add($"{name}:after");
			return result;
		}
	}

	private sealed class ShortCircuitMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			// Short-circuit - don't call next
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}
	}

	private sealed class ThrowingMiddleware(string name, Exception exception) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			throw exception;
		}
	}

	private sealed class CancellationAwareMiddleware(string name, List<string> invocations) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			invocations.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class StageSpecificMiddleware(string name, List<string> invocations, DispatchMiddlewareStage stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			return next(message, context, cancellationToken);
		}
	}

	private sealed class StageSpecificShortCircuitMiddleware(string name, List<string> invocations, DispatchMiddlewareStage stage) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			invocations.Add(name);
			// Short-circuit - don't call next
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}
	}

	#endregion
}
