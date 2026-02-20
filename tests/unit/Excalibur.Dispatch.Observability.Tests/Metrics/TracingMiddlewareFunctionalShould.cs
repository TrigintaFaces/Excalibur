// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Functional tests for <see cref="TracingMiddleware"/> verifying activity creation, tags, and status.
/// Uses unique message IDs per test to avoid Activity contamination from parallel xUnit execution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Tracing")]
public sealed class TracingMiddlewareFunctionalShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer = A.Fake<ITelemetrySanitizer>();
	private readonly ActivityListener _listener;
	private readonly ConcurrentBag<Activity> _capturedActivities = [];

	public TracingMiddlewareFunctionalShould()
	{
		_listener = new ActivityListener
		{
			// Exact match to avoid capturing activities from other Dispatch-related sources
			ShouldListenTo = source => source.Name == DispatchActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => _capturedActivities.Add(activity),
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		foreach (var activity in _capturedActivities)
		{
			activity.Dispose();
		}
	}

	/// <summary>
	/// Finds the single activity matching the given unique message ID.
	/// This isolates our test activity from contamination by parallel tests.
	/// </summary>
	private Activity FindActivityByMessageId(string messageId)
	{
		var matching = _capturedActivities
			.Where(a => a.GetTagItem("message.id")?.ToString() == messageId)
			.ToList();
		matching.ShouldHaveSingleItem();
		return matching[0];
	}

	[Fact]
	public async Task CreateActivity_WithMessageTypeTag()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		A.CallTo(() => context.CorrelationId).Returns("corr-456");

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.GetTagItem("message.type").ShouldNotBeNull();
		activity.GetTagItem("message.id").ShouldBe(uniqueId);
		activity.GetTagItem("dispatch.correlation.id").ShouldBe("corr-456");
		activity.GetTagItem("dispatch.operation").ShouldBe("handle");
	}

	[Fact]
	public async Task SetOkStatus_OnSuccessfulResult()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.Status.ShouldBe(ActivityStatusCode.Ok);
		activity.GetTagItem("dispatch.status").ShouldBe("success");
	}

	[Fact]
	public async Task SetErrorStatus_OnFailedResult()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(false);
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Validation failed");
		A.CallTo(() => problemDetails.Type).Returns("validation_error");
		A.CallTo(() => problemDetails.ErrorCode).Returns(1);
		A.CallTo(() => result.ProblemDetails).Returns(problemDetails);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.GetTagItem("dispatch.status").ShouldBe("failed");
		activity.GetTagItem("error.type").ShouldBe("validation_error");
		activity.GetTagItem("error.code").ShouldBe(1);
	}

	[Fact]
	public async Task SetExceptionStatus_OnException()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("boom");

		await Should.ThrowAsync<InvalidOperationException>(
			async () => await middleware.InvokeAsync(message, context, next, CancellationToken.None));

		var activity = FindActivityByMessageId(uniqueId);
		activity.GetTagItem("dispatch.status").ShouldBe("exception");
	}

	[Fact]
	public async Task TagHandlerType_WhenAvailableInContext()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(typeof(string));
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.GetTagItem("handler.type").ShouldBe("String");
	}

	[Fact]
	public async Task TagMessageKind_AsAction()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.GetTagItem("message.kind").ShouldBe("Action");
	}

	[Fact]
	public async Task TagMessageKind_AsEvent()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchEvent>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.GetTagItem("message.kind").ShouldBe("Event");
	}

	[Fact]
	public async Task TagMessageKind_AsDocument()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();
		var uniqueId = Guid.NewGuid().ToString();
		A.CallTo(() => context.MessageId).Returns(uniqueId);
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		var activity = FindActivityByMessageId(uniqueId);
		activity.GetTagItem("message.kind").ShouldBe("Document");
	}
}
