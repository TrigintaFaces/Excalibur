// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Pipeline;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class CancellationTokenPropagationShould
{
	private static IDispatchMiddleware CreateCapturingMiddleware(
		DispatchMiddlewareStage stage,
		Action<CancellationToken> onInvoke)
	{
		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(stage);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);

		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ct = call.GetArgument<CancellationToken>(3);
				onInvoke(ct);
				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				var msg = call.GetArgument<IDispatchMessage>(0)!;
				var ctx = call.GetArgument<IMessageContext>(1)!;
				return next(msg, ctx, ct);
			});

		return middleware;
	}

	[Fact]
	public async Task PropagateCancellationTokenToFinalDelegate()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		CancellationToken capturedToken = default;
		var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		ValueTask<IMessageResult> FinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
		{
			capturedToken = ct;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		await pipeline.ExecuteAsync(message, context, FinalDelegate, cts.Token);

		// Assert
		capturedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task PropagateCancellationTokenThroughSingleMiddleware()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		CancellationToken middlewareCapturedToken = default;
		CancellationToken finalCapturedToken = default;

		var middleware = CreateCapturingMiddleware(
			DispatchMiddlewareStage.PreProcessing,
			ct => middlewareCapturedToken = ct);

		var pipeline = new DispatchPipeline(new[] { middleware });
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act
		await pipeline.ExecuteAsync(message, context,
			(_, _, ct) =>
			{
				finalCapturedToken = ct;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			cts.Token);

		// Assert
		middlewareCapturedToken.ShouldBe(cts.Token);
		finalCapturedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task PropagateCancellationTokenThroughMultipleMiddleware()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var capturedTokens = new List<CancellationToken>();

		var pre = CreateCapturingMiddleware(
			DispatchMiddlewareStage.PreProcessing,
			ct => capturedTokens.Add(ct));

		var validation = CreateCapturingMiddleware(
			DispatchMiddlewareStage.Validation,
			ct => capturedTokens.Add(ct));

		var processing = CreateCapturingMiddleware(
			DispatchMiddlewareStage.Processing,
			ct => capturedTokens.Add(ct));

		var pipeline = new DispatchPipeline(new[] { pre, validation, processing });
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		CancellationToken finalCapturedToken = default;

		// Act
		await pipeline.ExecuteAsync(message, context,
			(_, _, ct) =>
			{
				finalCapturedToken = ct;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			cts.Token);

		// Assert
		capturedTokens.Count.ShouldBe(3);
		capturedTokens.ShouldAllBe(ct => ct == cts.Token);
		finalCapturedToken.ShouldBe(cts.Token);
	}

	[Fact]
	public async Task ThrowOperationCanceledWhenTokenCancelledDuringMiddleware()
	{
		// Arrange
		using var cts = new CancellationTokenSource();

		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
			{
				cts.Cancel();
				throw new OperationCanceledException(cts.Token);
			});

		var pipeline = new DispatchPipeline(new[] { middleware });
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => pipeline.ExecuteAsync(message, context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				cts.Token).AsTask());
	}

	[Fact]
	public async Task AllowMiddlewareToRespectCancellationAndStopProcessing()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Pre-cancel
		var secondMiddlewareCalled = false;

		var first = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => first.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => first.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => first.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ct = call.GetArgument<CancellationToken>(3);
				ct.ThrowIfCancellationRequested();
				return new ValueTask<IMessageResult>(MessageResult.Success());
			});

		var second = CreateCapturingMiddleware(
			DispatchMiddlewareStage.Validation,
			_ => secondMiddlewareCalled = true);

		var pipeline = new DispatchPipeline(new[] { first, second });
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => pipeline.ExecuteAsync(message, context,
				(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
				cts.Token).AsTask());

		secondMiddlewareCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task PropagateDifferentTokensForDifferentDispatches()
	{
		// Arrange
		using var cts1 = new CancellationTokenSource();
		using var cts2 = new CancellationTokenSource();
		var capturedTokens = new List<CancellationToken>();

		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				capturedTokens.Add(call.GetArgument<CancellationToken>(3));
				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				return next(
					call.GetArgument<IDispatchMessage>(0)!,
					call.GetArgument<IMessageContext>(1)!,
					call.GetArgument<CancellationToken>(3));
			});

		var pipeline = new DispatchPipeline(new[] { middleware });
		var message = A.Fake<IDispatchMessage>();

		// Act
		await pipeline.ExecuteAsync(message, new MessageContext(),
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			cts1.Token);

		await pipeline.ExecuteAsync(message, new MessageContext(),
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			cts2.Token);

		// Assert
		capturedTokens.Count.ShouldBe(2);
		capturedTokens[0].ShouldBe(cts1.Token);
		capturedTokens[1].ShouldBe(cts2.Token);
	}

	[Fact]
	public async Task PropagateNoneTokenWithoutError()
	{
		// Arrange
		CancellationToken capturedToken = new CancellationTokenSource().Token; // Non-None to verify override
		var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act
		await pipeline.ExecuteAsync(message, context,
			(_, _, ct) =>
			{
				capturedToken = ct;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None);

		// Assert
		capturedToken.ShouldBe(CancellationToken.None);
	}
}
