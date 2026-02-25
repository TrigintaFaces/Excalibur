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
/// Unit tests for <see cref="MiddlewareChainBuilder"/> and <see cref="ChainExecutor"/>.
/// Sprint 463 - S463.2: Tests for pre-compiled middleware chains (PERF-1).
/// Tests verify that chains are built at startup and execute without per-dispatch closures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
[Trait("Priority", "0")]
public sealed class MiddlewareChainBuilderShould : IDisposable
{
	private readonly ServiceProvider _serviceProvider;

	public MiddlewareChainBuilderShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();
	}

	public void Dispose()
	{
		_serviceProvider.Dispose();
	}

	#region ChainBuilder Tests

	[Fact]
	public void Constructor_WithValidMiddlewares_CreatesBuilder()
	{
		// Arrange & Act
		var builder = new MiddlewareChainBuilder(new[] { new TestMiddleware("Test", []) });

		// Assert
		_ = builder.ShouldNotBeNull();
		builder.IsFrozen.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithNullMiddlewares_CreatesEmptyBuilder()
	{
		// Arrange & Act
		var builder = new MiddlewareChainBuilder(null!);

		// Assert
		_ = builder.ShouldNotBeNull();
		var chain = builder.GetChain(typeof(TestMessage));
		chain.HasMiddleware.ShouldBeFalse();
	}

	[Fact]
	public void GetChain_ForMessageType_ReturnsChainExecutor()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });

		// Act
		var chain = builder.GetChain(typeof(TestMessage));

		// Assert
		_ = chain.ShouldNotBeNull();
		chain.HasMiddleware.ShouldBeTrue();
		chain.Count.ShouldBe(1);
	}

	[Fact]
	public void GetChain_SameMessageType_ReturnsCachedChain()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });

		// Act
		var chain1 = builder.GetChain(typeof(TestMessage));
		var chain2 = builder.GetChain(typeof(TestMessage));

		// Assert - Both should be the same cached instance
		chain1.ShouldBe(chain2);
	}

	[Fact]
	public void Freeze_MakesCacheImmutable()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });
		builder.IsFrozen.ShouldBeFalse();

		// Act
		builder.Freeze();

		// Assert
		builder.IsFrozen.ShouldBeTrue();
	}

	[Fact]
	public void Freeze_WithKnownTypes_PreCompilesChains()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });

		// Act
		builder.Freeze(new[] { typeof(TestMessage) });

		// Assert
		builder.IsFrozen.ShouldBeTrue();
		var chain = builder.GetChain(typeof(TestMessage));
		chain.HasMiddleware.ShouldBeTrue();
	}

	[Fact]
	public void Freeze_CalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });

		// Act & Assert - Should not throw
		builder.Freeze();
		builder.Freeze();
		builder.Freeze();

		builder.IsFrozen.ShouldBeTrue();
	}

	#endregion

	#region ChainExecutor Tests

	[Fact]
	public async Task ChainExecutor_Empty_InvokesFinalHandlerDirectly()
	{
		// Arrange
		var chain = ChainExecutor.Empty;
		var message = new TestMessage();
		var context = CreateContext();
		var finalHandlerCalled = false;

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
		{
			finalHandlerCalled = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None);

		// Assert
		finalHandlerCalled.ShouldBeTrue();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ChainExecutor_WithMiddleware_ExecutesChainInOrder()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", executionOrder),
			new TestMiddleware("Second", executionOrder),
			new TestMiddleware("Third", executionOrder),
		};
		var builder = new MiddlewareChainBuilder(middlewares);
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
		{
			executionOrder.Add("Final");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(new[] { "First", "Second", "Third", "Final" });
	}

	[Fact]
	public async Task ChainExecutor_WithTypedResponse_ReturnsCorrectType()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None);

		// Assert
		_ = result.ShouldBeAssignableTo<IMessageResult>();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ChainExecutor_MiddlewareCanShortCircuit()
	{
		// Arrange
		var executionOrder = new List<string>();
		var shortCircuitMiddleware = new ShortCircuitMiddleware();
		var middlewares = new IDispatchMiddleware[]
		{
			new TestMiddleware("First", executionOrder),
			shortCircuitMiddleware,
			new TestMiddleware("Third", executionOrder),
		};
		var builder = new MiddlewareChainBuilder(middlewares);
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
		{
			executionOrder.Add("Final");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		_ = await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None);

		// Assert - Only first middleware should execute, short circuit prevents rest
		executionOrder.ShouldBe(new[] { "First" });
	}

	[Fact]
	public async Task ChainExecutor_SupportsNestedDispatches()
	{
		// Arrange
		var executionOrder = new List<string>();
		var builder = new MiddlewareChainBuilder(new[] { new TestMiddleware("Outer", executionOrder) });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
		{
			executionOrder.Add("FinalOuter");

			// Simulate nested dispatch
			var innerBuilder = new MiddlewareChainBuilder(new[] { new TestMiddleware("Inner", executionOrder) });
			var innerChain = innerBuilder.GetChain(typeof(TestMessage));

			DispatchRequestDelegate innerHandler = static (m, c, t) =>
				new ValueTask<IMessageResult>(MessageResult.Success());

			return innerChain.InvokeAsync(msg, ctx, innerHandler, ct);
		};

		// Act
		_ = await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(new[] { "Outer", "FinalOuter", "Inner" });
	}

	[Fact]
	public async Task ChainExecutor_PropagatesExceptions()
	{
		// Arrange
		var throwingMiddleware = new ThrowingMiddleware();
		var builder = new MiddlewareChainBuilder(new[] { throwingMiddleware });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None));
	}

	[Fact]
	public async Task ChainExecutor_PassesCancellationToken()
	{
		// Arrange
		CancellationToken? receivedToken = null;
		var tokenCheckMiddleware = new TokenCheckMiddleware(token => receivedToken = token);
		var builder = new MiddlewareChainBuilder(new[] { tokenCheckMiddleware });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();
		using var cts = new CancellationTokenSource();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		_ = await chain.InvokeAsync(message, context, finalHandler, cts.Token);

		// Assert
		_ = receivedToken.ShouldNotBeNull();
		receivedToken.Value.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task ChainExecutor_CanHandleDeepChain()
	{
		// Arrange - Create a deep chain to stress-test the pre-compiled chain
		const int middlewareCount = 100;
		var executionOrder = new List<string>();
		var middlewares = Enumerable.Range(0, middlewareCount)
			.Select(i => new TestMiddleware($"Middleware-{i}", executionOrder))
			.Cast<IDispatchMiddleware>()
			.ToArray();

		var builder = new MiddlewareChainBuilder(middlewares);
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		DispatchRequestDelegate finalHandler = (msg, ctx, ct) =>
		{
			executionOrder.Add("Final");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act
		var result = await chain.InvokeAsync(message, context, finalHandler, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		executionOrder.Count.ShouldBe(middlewareCount + 1);
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

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			executionOrder.Add(name);
			return await nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ShortCircuitMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Don't call next - short circuit
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}
	}

	private sealed class ThrowingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("Test exception");
		}
	}

	private sealed class TokenCheckMiddleware(Action<CancellationToken> tokenReceiver) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			tokenReceiver(cancellationToken);
			return nextDelegate(message, context, cancellationToken);
		}
	}

	#endregion
}
