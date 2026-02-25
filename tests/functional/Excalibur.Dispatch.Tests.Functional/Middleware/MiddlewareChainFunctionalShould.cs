// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using DispatchRequestDelegate = Excalibur.Dispatch.Abstractions.DispatchRequestDelegate;
using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Functional.Middleware;

/// <summary>
/// Functional tests for middleware chain patterns in dispatch pipeline.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Middleware")]
[Trait("Feature", "Pipeline")]
public sealed class MiddlewareChainFunctionalShould : FunctionalTestBase
{
	[Fact]
	public async Task ExecuteMiddlewareInCorrectOrder()
	{
		// Arrange
		var executionOrder = new ConcurrentQueue<string>();

		var middleware1 = new TrackingMiddleware("First", executionOrder);
		var middleware2 = new TrackingMiddleware("Second", executionOrder);
		var middleware3 = new TrackingMiddleware("Third", executionOrder);

		var message = new TestMessage();
		var context = CreateTestContext(message);

		// Act - Build and execute pipeline
		ValueTask<IMessageResult> FinalHandler(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			executionOrder.Enqueue("Handler");
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Execute chain manually (simulating pipeline)
		_ = await middleware1.InvokeAsync(
			message,
			context,
			async (m1, c1, t1) => await middleware2.InvokeAsync(
				m1,
				c1,
				async (m2, c2, t2) => await middleware3.InvokeAsync(
					m2,
					c2,
					FinalHandler,
					t2).ConfigureAwait(false),
				t1).ConfigureAwait(false),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		var order = executionOrder.ToArray();
		order.Length.ShouldBe(7); // 3 before + handler + 3 after
		order[0].ShouldBe("First-Before");
		order[1].ShouldBe("Second-Before");
		order[2].ShouldBe("Third-Before");
		order[3].ShouldBe("Handler");
		order[4].ShouldBe("Third-After");
		order[5].ShouldBe("Second-After");
		order[6].ShouldBe("First-After");
	}

	[Fact]
	public async Task ShortCircuitPipelineOnError()
	{
		// Arrange
		var executionOrder = new ConcurrentQueue<string>();
		var errorThrown = false;

		var middleware1 = new TrackingMiddleware("First", executionOrder);
		var errorMiddleware = new ErrorMiddleware(executionOrder);
		var middleware3 = new TrackingMiddleware("Third", executionOrder);

		var message = new TestMessage();
		var context = CreateTestContext(message);

		// Act
		try
		{
			ValueTask<IMessageResult> FinalHandler(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
			{
				executionOrder.Enqueue("Handler");
				return new ValueTask<IMessageResult>(MessageResult.Success());
			}

			_ = await middleware1.InvokeAsync(
				message,
				context,
				async (m1, c1, t1) => await errorMiddleware.InvokeAsync(
					m1,
					c1,
					async (m2, c2, t2) => await middleware3.InvokeAsync(
						m2,
						c2,
						FinalHandler,
						t2).ConfigureAwait(false),
					t1).ConfigureAwait(false),
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			errorThrown = true;
		}

		// Assert
		errorThrown.ShouldBeTrue();
		var order = executionOrder.ToArray();
		order.ShouldContain("First-Before");
		order.ShouldContain("Error-Before");
		order.ShouldNotContain("Third-Before");
		order.ShouldNotContain("Handler");
	}

	[Fact]
	public async Task PassContextBetweenMiddleware()
	{
		// Arrange
		var message = new TestMessage();
		var context = CreateTestContext(message);

		// Middleware that adds to context
		async ValueTask<IMessageResult> AddToContext(
			IDispatchMessage msg,
			IMessageContext ctx,
			DispatchRequestDelegate next,
			CancellationToken ct)
		{
			ctx.Items["Key1"] = "Value1";
			var result = await next(msg, ctx, ct).ConfigureAwait(false);
			return result;
		}

		// Middleware that reads from context
		ValueTask<IMessageResult> ReadFromContext(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			var value = ctx.Items["Key1"] as string;
			ctx.Items["ReadValue"] = value;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		_ = await AddToContext(message, context, ReadFromContext, CancellationToken.None).ConfigureAwait(false);

		// Assert
		context.Items["Key1"].ShouldBe("Value1");
		context.Items["ReadValue"].ShouldBe("Value1");
	}

	[Fact]
	public async Task MeasureMiddlewareExecutionTime()
	{
		// Arrange
		var timings = new ConcurrentDictionary<string, TimeSpan>();

		async ValueTask<IMessageResult> TimingMiddleware(
			string name,
			IDispatchMessage msg,
			IMessageContext ctx,
			DispatchRequestDelegate next,
			CancellationToken ct)
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var result = await next(msg, ctx, ct).ConfigureAwait(false);
			sw.Stop();
			timings[name] = sw.Elapsed;
			return result;
		}

		var message = new TestMessage();
		var context = CreateTestContext(message);

		// Act
		_ = await TimingMiddleware(
			"Outer",
			message,
			context,
			async (m1, c1, t1) => await TimingMiddleware(
				"Inner",
				m1,
				c1,
				async (m2, c2, t2) =>
				{
					await Task.Delay(10, t2).ConfigureAwait(false);
					return MessageResult.Success();
				},
				t1).ConfigureAwait(false),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		timings.ContainsKey("Outer").ShouldBeTrue();
		timings.ContainsKey("Inner").ShouldBeTrue();
		timings["Outer"].ShouldBeGreaterThanOrEqualTo(timings["Inner"]);
	}

	[Fact]
	public async Task HandleCancellationInMiddleware()
	{
		// Arrange
		var cancelled = false;
		var message = new TestMessage();
		var context = CreateTestContext(message);

		async ValueTask<IMessageResult> CancellationAwareMiddleware(
			IDispatchMessage msg,
			IMessageContext ctx,
			DispatchRequestDelegate next,
			CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			await Task.Delay(100, ct).ConfigureAwait(false);
			return await next(msg, ctx, ct).ConfigureAwait(false);
		}

		// Use a pre-cancelled token to make this deterministic under load.
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		try
		{
			_ = await CancellationAwareMiddleware(
				message,
				context,
				(m, c, t) => new ValueTask<IMessageResult>(MessageResult.Success()),
				cts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			cancelled = true;
		}

		// Assert
		cancelled.ShouldBeTrue();
	}

	[Fact]
	public async Task SupportConditionalMiddleware()
	{
		// Arrange
		var conditionalExecuted = false;
		var message = new TestMessage { ShouldExecuteConditional = true };
		var context = CreateTestContext(message);

		async ValueTask<IMessageResult> ConditionalMiddleware(
			IDispatchMessage msg,
			IMessageContext ctx,
			DispatchRequestDelegate next,
			CancellationToken ct)
		{
			if (msg is TestMessage tm && tm.ShouldExecuteConditional)
			{
				conditionalExecuted = true;
			}

			return await next(msg, ctx, ct).ConfigureAwait(false);
		}

		// Act
		_ = await ConditionalMiddleware(
			message,
			context,
			(m, c, t) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		conditionalExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task TransformMessageInMiddleware()
	{
		// Arrange
		var message = new TransformableMessage { Value = 10 };
		var context = CreateTestContext(message);
		var transformedValue = 0;

		async ValueTask<IMessageResult> TransformMiddleware(
			IDispatchMessage msg,
			IMessageContext ctx,
			DispatchRequestDelegate next,
			CancellationToken ct)
		{
			if (msg is TransformableMessage tm)
			{
				// Transform the message
				var transformed = tm with { Value = tm.Value * 2 };
				return await next(transformed, ctx, ct).ConfigureAwait(false);
			}

			return await next(msg, ctx, ct).ConfigureAwait(false);
		}

		// Act
		_ = await TransformMiddleware(
			message,
			context,
			(m, c, t) =>
			{
				if (m is TransformableMessage tm)
				{
					transformedValue = tm.Value;
				}

				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		transformedValue.ShouldBe(20);
	}

	private static MessageContext CreateTestContext(IDispatchMessage message)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var provider = services.BuildServiceProvider();
		return new MessageContext(message, provider);
	}

	private sealed record TestMessage : IDispatchMessage
	{
		public bool ShouldExecuteConditional { get; init; }
	}

	private sealed record TransformableMessage : IDispatchMessage
	{
		public int Value { get; init; }
	}

	private sealed class TrackingMiddleware(string name, ConcurrentQueue<string> executionOrder)
	{
		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			executionOrder.Enqueue($"{name}-Before");
			var result = await next(message, context, cancellationToken).ConfigureAwait(false);
			executionOrder.Enqueue($"{name}-After");
			return result;
		}
	}

	private sealed class ErrorMiddleware(ConcurrentQueue<string> executionOrder)
	{
		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate next,
			CancellationToken cancellationToken)
		{
			executionOrder.Enqueue("Error-Before");
			throw new InvalidOperationException("Middleware error");
		}
	}
}
