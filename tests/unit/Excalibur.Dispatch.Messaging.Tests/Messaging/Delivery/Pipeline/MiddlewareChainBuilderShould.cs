// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA1861 // Prefer 'static readonly' fields - acceptable in tests

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Pipeline;
using Tests.Shared.TestFakes;

using Microsoft.Extensions.DependencyInjection;

using ShouldlyCase = Shouldly.Case;
using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for <see cref="MiddlewareChainBuilder"/> and <see cref="ChainExecutor"/>.
/// Sprint 463 - S463.2: Tests for pre-compiled middleware chains (PERF-1).
/// Tests verify that chains are built at startup and execute without per-dispatch closures.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
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
	public void GetChain_AfterFreeze_ForUnknownType_UsesCachedFallbackChain()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });
		builder.Freeze(new[] { typeof(TestMessage) });

		// Act
		var first = builder.GetChain(typeof(UnknownMessage));
		var second = builder.GetChain(typeof(UnknownMessage));

		// Assert
		first.HasMiddleware.ShouldBeTrue();
		first.ShouldBe(second);
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

	/// <summary>
	/// SAFETY, and the guarantee that until now had no arm on the path a dispatched message takes: a
	/// middleware declaring a LOWER stage runs before one declaring a higher stage, whatever order they
	/// were added in.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this was missing.</b> Every other fixture in this file declares <c>Stage =&gt; null</c>, so
	/// each element keys to the same value and the sort is never handed two different keys. Delete the
	/// body of the comparison loop in <c>SortByStageInPlace</c> and the rest of this file still passes.
	/// Tests elsewhere do assert cross-stage ordering, but they construct <c>DispatchPipeline</c>
	/// directly, and no dispatched message reaches that object because the dispatcher consults the
	/// invoker. So those assertions are aimed at a type the dispatcher does not use.
	/// </para>
	/// <para>
	/// <b>Non-vacuity.</b> The mutant is making <c>SortByStageInPlace</c> a no-op, which leaves
	/// registration order intact. This arm goes RED; the null-staged arms above stay GREEN, because
	/// nothing distinguishes their keys. That split is what the mutant proof needs. A mutant that
	/// reddened everything would only show the chain runs at all.
	/// </para>
	/// <para>
	/// The stages are non-adjacent and registered in exactly reverse order, so a sort that is merely
	/// unstable, or that compares only neighbouring pairs, cannot produce this sequence by accident.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task ChainExecutor_RunsALowerStageFirst_WhateverOrderTheyWereAddedIn()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new StagedTestMiddleware("PostProcessing", DispatchMiddlewareStage.PostProcessing, executionOrder),
			new StagedTestMiddleware("Processing", DispatchMiddlewareStage.Processing, executionOrder),
			new StagedTestMiddleware("Authorization", DispatchMiddlewareStage.Authorization, executionOrder),
			new StagedTestMiddleware("Validation", DispatchMiddlewareStage.Validation, executionOrder),
			new StagedTestMiddleware("Start", DispatchMiddlewareStage.Start, executionOrder),
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
		executionOrder.ShouldBe(
			new[] { "Start", "Validation", "Authorization", "Processing", "PostProcessing", "Final" },
			ShouldlyCase.Sensitive,
			"stage decides across stages. These were added in reverse stage order, so registration order "
			+ "surviving anywhere in this sequence means the sort on the executing path is not ordering them.");
	}

	/// <summary>
	/// A middleware declaring NO stage runs last, because an absent stage is treated as <c>End</c> and
	/// not as the place it happened to be added.
	/// </summary>
	/// <remarks>
	/// This is the half a consumer is most likely to get wrong, since an unstaged middleware added first
	/// looks like it should run first. It is also what makes behaviour predictable when a consumer mixes
	/// staged framework middleware with their own unstaged ones.
	/// </remarks>
	[Fact]
	public async Task ChainExecutor_RunsAnUnstagedMiddlewareLast_EvenWhenItWasAddedFirst()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new TestMiddleware("Unstaged", executionOrder),
			new StagedTestMiddleware("Start", DispatchMiddlewareStage.Start, executionOrder),
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
		executionOrder.ShouldBe(
			new[] { "Start", "Unstaged", "Final" },
			ShouldlyCase.Sensitive,
			"a middleware declaring no stage is treated as End, so it runs after every staged one however "
			+ "early it was added.");
	}

	/// <summary>
	/// LIVENESS, and the arm that stops the two above being satisfied by a sort that reorders
	/// everything: two middleware sharing a stage keep the order they were added in.
	/// </summary>
	/// <remarks>
	/// The null-staged arms cover this for the default key. This one covers it for a REAL key, so the
	/// stability of the sort is bound where the comparison actually runs. The mutant is the strict
	/// <c>&gt;</c> in <c>SortByStageInPlace</c> becoming <c>&gt;=</c>, which makes the insertion sort
	/// swap equal keys and reverses this pair.
	/// </remarks>
	[Fact]
	public async Task ChainExecutor_KeepsRegistrationOrder_WithinASingleStage()
	{
		// Arrange
		var executionOrder = new List<string>();
		var middlewares = new IDispatchMiddleware[]
		{
			new StagedTestMiddleware("FirstAdded", DispatchMiddlewareStage.Validation, executionOrder),
			new StagedTestMiddleware("SecondAdded", DispatchMiddlewareStage.Validation, executionOrder),
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
		executionOrder.ShouldBe(
			new[] { "FirstAdded", "SecondAdded", "Final" },
			ShouldlyCase.Sensitive,
			"within one stage the order belongs to the caller. A sort that swaps equal keys takes away the "
			+ "only ordering control they have inside a stage. "
			+ "ordering control they have inside a stage.");
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
	public async Task ChainExecutor_InvokeTyped_WithSyncFinalHandler_ReturnsTypedResult()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		// Act
		var result = await chain.InvokeAsync(
			message,
			context,
			static (_, _, _) => ValueTask.FromResult(new TypedTestResult("sync")),
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.Marker.ShouldBe("sync");
	}

	[Fact]
	public async Task ChainExecutor_InvokeTyped_WithAsyncFinalHandler_ReturnsTypedResult()
	{
		// Arrange
		var middleware = new TestMiddleware("Test", []);
		var builder = new MiddlewareChainBuilder(new[] { middleware });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		static async ValueTask<TypedTestResult> AsyncHandler()
		{
			await Task.Yield();
			return new TypedTestResult("async");
		}

		// Act
		var result = await chain.InvokeAsync(
			message,
			context,
			static (_, _, _) => AsyncHandler(),
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.Marker.ShouldBe("async");
	}

	[Fact]
	public async Task ChainExecutor_InvokeTyped_WithSuppressedExecutionContextFlow_UsesContextFallback()
	{
		// Arrange
		var builder = new MiddlewareChainBuilder(new IDispatchMiddleware[] { new SuppressFlowMiddleware() });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = new Excalibur.Dispatch.Messaging.MessageContext(message, _serviceProvider);

		// Act
		var result = await chain.InvokeAsync(
			message,
			context,
			static (_, _, _) => ValueTask.FromResult(new TypedTestResult("suppressed-flow")),
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.Marker.ShouldBe("suppressed-flow");
	}

	[Fact]
	public async Task ChainExecutor_Invoke_WithSuppressedExecutionContextFlow_UsesContextFallback()
	{
		// Arrange
		var builder = new MiddlewareChainBuilder(new IDispatchMiddleware[] { new SuppressFlowMiddleware() });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = new Excalibur.Dispatch.Messaging.MessageContext(message, _serviceProvider);
		DispatchRequestDelegate finalHandler = static (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act
		var result = await chain.InvokeAsync(
			message,
			context,
			finalHandler,
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ChainExecutor_InvokeTyped_SupportsNestedTypedDispatches()
	{
		// Arrange
		var builder = new MiddlewareChainBuilder(new[] { new TestMiddleware("Outer", []) });
		var chain = builder.GetChain(typeof(TestMessage));
		var message = new TestMessage();
		var context = CreateContext();

		static async ValueTask<TypedTestResult> OuterHandler(IDispatchMessage outerMessage, IMessageContext outerContext, CancellationToken token)
		{
			var innerBuilder = new MiddlewareChainBuilder(new[] { new TestMiddleware("Inner", []) });
			var innerChain = innerBuilder.GetChain(typeof(TestMessage));
			var innerResult = await innerChain.InvokeAsync(
				outerMessage,
				outerContext,
				static (_, _, _) => ValueTask.FromResult(new TypedTestResult("inner")),
				token);

			return new TypedTestResult($"outer:{innerResult.Marker}");
		}

		// Act
		var result = await chain.InvokeAsync(message, context, OuterHandler, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.Marker.ShouldBe("outer:inner");
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

	private sealed record UnknownMessage : IDispatchMessage;

	private sealed class TypedTestResult(string marker) : IMessageResult
	{
		public string Marker { get; } = marker;
		public bool Succeeded => true;
		public string? ErrorMessage => null;
		public object? ValidationResult => null;
		public object? AuthorizationResult => null;
		public IMessageProblemDetails? ProblemDetails => null;
	}

	/// <summary>
	/// A middleware that DECLARES a stage, so the sort on the executing path is handed two different
	/// keys. Implements the interface directly, inheriting no first-party base that could supply the
	/// ordering behaviour under test.
	/// </summary>
	private sealed class StagedTestMiddleware(
		string name,
		DispatchMiddlewareStage stage,
		List<string> executionOrder) : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => stage;
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

	private sealed class SuppressFlowMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => null;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			using (ExecutionContext.SuppressFlow())
			{
				return new ValueTask<IMessageResult>(
					Task.Run(async () => await nextDelegate(message, context, cancellationToken).ConfigureAwait(false), cancellationToken));
			}
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
