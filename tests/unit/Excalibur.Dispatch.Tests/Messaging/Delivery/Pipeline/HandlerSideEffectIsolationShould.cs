// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Pipeline;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class HandlerSideEffectIsolationShould
{
	[Fact]
	public async Task ProvideIsolatedContextPerDispatch()
	{
		// Arrange
		var capturedContexts = new ConcurrentBag<IMessageContext>();

		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ctx = call.GetArgument<IMessageContext>(1)!;
				ctx.Items["marker"] = Guid.NewGuid().ToString();
				capturedContexts.Add(ctx);
				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				return next(
					call.GetArgument<IDispatchMessage>(0)!,
					ctx,
					call.GetArgument<CancellationToken>(3));
			});

		var pipeline = new DispatchPipeline(new[] { middleware });
		var message = A.Fake<IDispatchMessage>();

		// Act -- dispatch 3 times with separate contexts
		for (var i = 0; i < 3; i++)
		{
			await pipeline.ExecuteAsync(message, new MessageContext(),
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None);
		}

		// Assert -- each dispatch should have its own context instance
		var contexts = capturedContexts.ToArray();
		contexts.Length.ShouldBe(3);

		// All contexts should be different instances
		contexts[0].ShouldNotBeSameAs(contexts[1]);
		contexts[1].ShouldNotBeSameAs(contexts[2]);
		contexts[0].ShouldNotBeSameAs(contexts[2]);
	}

	[Fact]
	public async Task NotLeakMiddlewareStateAcrossDispatches()
	{
		// Arrange -- middleware that tracks invocation count via context Items
		var invocationResults = new ConcurrentBag<int>();

		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ctx = call.GetArgument<IMessageContext>(1)!;

				// Each dispatch should start fresh -- context Items should not have "counter" yet
				var counter = ctx.Items.TryGetValue("counter", out var existing) ? (int)existing : 0;
				counter++;
				ctx.Items["counter"] = counter;
				invocationResults.Add(counter);

				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				return next(
					call.GetArgument<IDispatchMessage>(0)!,
					ctx,
					call.GetArgument<CancellationToken>(3));
			});

		var pipeline = new DispatchPipeline(new[] { middleware });
		var message = A.Fake<IDispatchMessage>();

		// Act -- dispatch 5 times with fresh contexts
		for (var i = 0; i < 5; i++)
		{
			await pipeline.ExecuteAsync(message, new MessageContext(),
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None);
		}

		// Assert -- each invocation should see counter=1 (no leakage from prior dispatches)
		invocationResults.Count.ShouldBe(5);
		invocationResults.ShouldAllBe(count => count == 1);
	}

	[Fact]
	public async Task IsolateConcurrentDispatchSideEffects()
	{
		// Arrange
		var capturedValues = new ConcurrentBag<string>();

		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				var ctx = call.GetArgument<IMessageContext>(1)!;
				var value = Guid.NewGuid().ToString();
				ctx.Items["unique"] = value;

				// Simulate some async work
				await Task.Yield();

				// Read back -- should be the same value we set (no cross-dispatch contamination)
				ctx.Items["unique"].ShouldBe(value);
				capturedValues.Add(value);

				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				return await next(
					call.GetArgument<IDispatchMessage>(0)!,
					ctx,
					call.GetArgument<CancellationToken>(3)).ConfigureAwait(false);
			});

		var pipeline = new DispatchPipeline(new[] { middleware });
		var message = A.Fake<IDispatchMessage>();

		// Act -- dispatch concurrently
		var tasks = Enumerable.Range(0, 10).Select(_ =>
			pipeline.ExecuteAsync(message, new MessageContext(),
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				CancellationToken.None).AsTask());

		await Task.WhenAll(tasks);

		// Assert -- all 10 dispatches completed with unique values
		capturedValues.Count.ShouldBe(10);
		capturedValues.Distinct().Count().ShouldBe(10); // All unique = no cross-contamination
	}

	[Fact]
	public async Task NotShareResultsBetweenDispatches()
	{
		// Arrange
		var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());
		var message = A.Fake<IDispatchMessage>();
		var results = new List<IMessageResult>();

		// Act -- dispatch 3 times, each with a different result
		for (var i = 0; i < 3; i++)
		{
			var captured = i;
			var result = await pipeline.ExecuteAsync(message, new MessageContext(),
				(_, _, _) => new ValueTask<IMessageResult>(
					MessageResult.Success($"Result-{captured}")),
				CancellationToken.None);
			results.Add(result);
		}

		// Assert -- each result is independent
		results.Count.ShouldBe(3);
		results[0].ShouldNotBeSameAs(results[1]);
		results[1].ShouldNotBeSameAs(results[2]);
	}
}
