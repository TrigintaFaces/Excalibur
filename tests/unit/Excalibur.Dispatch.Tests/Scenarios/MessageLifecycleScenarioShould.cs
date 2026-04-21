// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Scenarios;

/// <summary>
/// E2E scenario tests exercising the full Command→Pipeline→Middleware→Handler→Result message lifecycle.
/// Uses the framework's real pipeline implementation with fake middleware/handlers to verify
/// the complete dispatch flow without external infrastructure.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class MessageLifecycleScenarioShould
{
	private sealed class TestCommand : IDispatchMessage
	{
		public string OrderId { get; init; } = string.Empty;
	}

	#region Full Pipeline Lifecycle

	[Fact]
	public async Task ExecuteFullPipelineFromDispatchToHandler()
	{
		// Arrange -- build a real pipeline with middleware chain
		var executionLog = new List<string>();

		var preProcessing = CreateLoggingMiddleware(DispatchMiddlewareStage.PreProcessing, "PreProcessing", executionLog);
		var validation = CreateLoggingMiddleware(DispatchMiddlewareStage.Validation, "Validation", executionLog);
		var authorization = CreateLoggingMiddleware(DispatchMiddlewareStage.Authorization, "Authorization", executionLog);
		var serialization = CreateLoggingMiddleware(DispatchMiddlewareStage.Serialization, "Serialization", executionLog);
		var processing = CreateLoggingMiddleware(DispatchMiddlewareStage.Processing, "Processing", executionLog);

		var pipeline = new DispatchPipeline(new[] { preProcessing, validation, authorization, serialization, processing });
		var command = new TestCommand { OrderId = "ORD-001" };
		var context = new MessageContext { MessageId = "msg-001", CorrelationId = "corr-001" };

		// Act -- dispatch through the full pipeline
		var result = await pipeline.ExecuteAsync(
			command,
			context,
			(msg, ctx, ct) =>
			{
				executionLog.Add("Handler");
				return new ValueTask<IMessageResult>(MessageResult.Success("Order processed"));
			},
			CancellationToken.None);

		// Assert -- verify correct execution order and result
		result.Succeeded.ShouldBeTrue();

		executionLog.Count.ShouldBe(6); // 5 middleware + 1 handler

		// Verify middleware executed in numeric stage order (enum values)
		// PreProcessing(10) < Validation(200) < Serialization(250) < Authorization(300) < Processing(400)
		executionLog[0].ShouldBe("PreProcessing");
		executionLog[1].ShouldBe("Validation");
		executionLog[2].ShouldBe("Serialization");
		executionLog[3].ShouldBe("Authorization");
		executionLog[4].ShouldBe("Processing");
		executionLog[5].ShouldBe("Handler");
	}

	[Fact]
	public async Task PropagateContextThroughEntirePipeline()
	{
		// Arrange -- middleware enriches context, handler reads it
		string? handlerReadCorrelationId = null;
		string? handlerReadCustomValue = null;

		var enrichingMiddleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => enrichingMiddleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => enrichingMiddleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => enrichingMiddleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				var ctx = call.GetArgument<IMessageContext>(1)!;
				ctx.Items["tenant"] = "acme-corp";
				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				return next(call.GetArgument<IDispatchMessage>(0)!, ctx, call.GetArgument<CancellationToken>(3));
			});

		var pipeline = new DispatchPipeline(new[] { enrichingMiddleware });
		var context = new MessageContext { CorrelationId = "corr-lifecycle" };

		// Act
		await pipeline.ExecuteAsync(
			new TestCommand { OrderId = "ORD-002" },
			context,
			(msg, ctx, ct) =>
			{
				handlerReadCorrelationId = ctx.CorrelationId;
				handlerReadCustomValue = ctx.Items.TryGetValue("tenant", out var val) ? (string)val : null;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None);

		// Assert -- handler received the enriched context
		handlerReadCorrelationId.ShouldBe("corr-lifecycle");
		handlerReadCustomValue.ShouldBe("acme-corp");
	}

	#endregion

	#region Validation Short-Circuit Scenario

	[Fact]
	public async Task ShortCircuitAtValidationAndSkipHandler()
	{
		// Arrange -- validation middleware rejects the message
		var handlerCalled = false;

		var validation = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => validation.Stage).Returns(DispatchMiddlewareStage.Validation);
		A.CallTo(() => validation.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => validation.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(_ =>
				new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
				{
					Type = "validation-error",
					Title = "Invalid Order",
					ErrorCode = 400,
					Status = 400,
					Detail = "OrderId is required",
				})));

		var pipeline = new DispatchPipeline(new[] { validation });

		// Act
		var result = await pipeline.ExecuteAsync(
			new TestCommand { OrderId = "" },
			new MessageContext(),
			(_, _, _) =>
			{
				handlerCalled = true;
				return new ValueTask<IMessageResult>(MessageResult.Success());
			},
			CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		handlerCalled.ShouldBeFalse();
	}

	#endregion

	#region Error Handling Scenario

	[Fact]
	public async Task WrapHandlerExceptionInMiddleware()
	{
		// Arrange -- error handling middleware catches handler exception
		var errorMiddleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => errorMiddleware.Stage).Returns(DispatchMiddlewareStage.ErrorHandling);
		A.CallTo(() => errorMiddleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => errorMiddleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(async call =>
			{
				try
				{
					var next = call.GetArgument<DispatchRequestDelegate>(2)!;
					return await next(
						call.GetArgument<IDispatchMessage>(0)!,
						call.GetArgument<IMessageContext>(1)!,
						call.GetArgument<CancellationToken>(3));
				}
				catch (InvalidOperationException ex)
				{
					return MessageResult.Failed(new MessageProblemDetails
					{
						Type = "handler-error",
						Title = ex.Message,
						ErrorCode = 500,
						Status = 500,
					});
				}
			});

		var pipeline = new DispatchPipeline(new[] { errorMiddleware });

		// Act -- handler throws
		var result = await pipeline.ExecuteAsync(
			new TestCommand(),
			new MessageContext(),
			(_, _, _) => throw new InvalidOperationException("Order processing failed"),
			CancellationToken.None);

		// Assert -- error was caught by middleware and returned as failed result
		result.Succeeded.ShouldBeFalse();
	}

	#endregion

	#region Concurrent Dispatch Scenario

	[Fact]
	public async Task HandleConcurrentDispatchesWithIsolation()
	{
		// Arrange
		var results = new ConcurrentBag<(string OrderId, string ContextId)>();
		var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());

		// Act -- dispatch 20 commands concurrently
		var tasks = Enumerable.Range(1, 20).Select(i =>
		{
			var command = new TestCommand { OrderId = $"ORD-{i:D3}" };
			var context = new MessageContext { MessageId = $"msg-{i:D3}" };

			return pipeline.ExecuteAsync(
				command,
				context,
				(msg, ctx, ct) =>
				{
					var cmd = (TestCommand)msg;
					results.Add((cmd.OrderId, ctx.MessageId!));
					return new ValueTask<IMessageResult>(MessageResult.Success());
				},
				CancellationToken.None).AsTask();
		}).ToArray();

		await Task.WhenAll(tasks);

		// Assert -- all 20 completed with correct mapping
		results.Count.ShouldBe(20);
		var ordered = results.OrderBy(r => r.OrderId).ToList();
		for (var i = 0; i < 20; i++)
		{
			var expected = $"ORD-{i + 1:D3}";
			ordered[i].OrderId.ShouldBe(expected);
			ordered[i].ContextId.ShouldBe($"msg-{i + 1:D3}");
		}
	}

	#endregion

	#region Cancellation Mid-Lifecycle Scenario

	[Fact]
	public async Task CancelMidLifecycleAndNotReachHandler()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var handlerReached = false;

		var slowMiddleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => slowMiddleware.Stage).Returns(DispatchMiddlewareStage.PreProcessing);
		A.CallTo(() => slowMiddleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => slowMiddleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				// Cancel during middleware execution
				cts.Cancel();
				call.GetArgument<CancellationToken>(3).ThrowIfCancellationRequested();
				return new ValueTask<IMessageResult>(MessageResult.Success());
			});

		var pipeline = new DispatchPipeline(new[] { slowMiddleware });

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => pipeline.ExecuteAsync(
				new TestCommand(),
				new MessageContext(),
				(_, _, _) =>
				{
					handlerReached = true;
					return new ValueTask<IMessageResult>(MessageResult.Success());
				},
				cts.Token).AsTask());

		handlerReached.ShouldBeFalse();
	}

	#endregion

	#region Helpers

	private static IDispatchMiddleware CreateLoggingMiddleware(
		DispatchMiddlewareStage stage,
		string name,
		List<string> log)
	{
		var middleware = A.Fake<IDispatchMiddleware>();
		A.CallTo(() => middleware.Stage).Returns(stage);
		A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);
		A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				log.Add(name);
				var next = call.GetArgument<DispatchRequestDelegate>(2)!;
				return next(
					call.GetArgument<IDispatchMessage>(0)!,
					call.GetArgument<IMessageContext>(1)!,
					call.GetArgument<CancellationToken>(3));
			});
		return middleware;
	}

	#endregion
}
