// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// In-depth unit tests for <see cref="TracingMiddleware"/> covering uncovered code paths.
/// Uses unique message IDs per test to avoid Activity contamination from parallel xUnit execution.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class TracingMiddlewareDepthShould
{
	private static IOptions<ObservabilityOptions> DefaultOptions =>
		Microsoft.Extensions.Options.Options.Create(new ObservabilityOptions { EnableDetailedTiming = true, IncludeSensitiveData = true });

	/// <summary>
	/// Creates a fake <see cref="IMessageContext"/> backed by a real Items dictionary
	/// so that extension methods (GetItem, SetItem, ContainsItem) work correctly.
	/// </summary>
	private static IMessageContext CreateFakeContext(string? messageId = null, string? correlationId = null, Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		var itemsDict = items ?? new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(itemsDict);
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		if (messageId is not null)
		{
			A.CallTo(() => context.MessageId).Returns(messageId);
		}
		if (correlationId is not null)
		{
			A.CallTo(() => context.CorrelationId).Returns(correlationId);
		}
		return context;
	}

	/// <summary>
	/// Creates a per-test activity listener that captures dispatch activities,
	/// invokes the middleware, and returns the captured activity for assertions.
	/// Uses a unique message ID to isolate from parallel test contamination.
	/// </summary>
	private static async Task<Activity?> InvokeAndCaptureActivity(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate next,
		ITelemetrySanitizer? sanitizer = null)
	{
		var captured = new ConcurrentBag<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == DispatchActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => captured.Add(activity),
		};
		ActivitySource.AddActivityListener(listener);

		// Get the unique message ID set by the test for filtering
		var messageId = context.MessageId;

		var middleware = new TracingMiddleware(DefaultOptions, sanitizer ?? A.Fake<ITelemetrySanitizer>());
		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Find the activity matching OUR unique message ID to avoid cross-test interference
		return captured.FirstOrDefault(a => a.GetTagItem("message.id")?.ToString() == messageId);
	}

	[Fact]
	public async Task SetOkStatus_OnSuccessResult()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId, correlationId: "corr-1");

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("success");
		activity.Status.ShouldBe(ActivityStatusCode.Ok);
		activity.Dispose();
	}

	[Fact]
	public async Task SetErrorStatus_OnFailedResult()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.IsSuccess).Returns(false);
		var pd = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => pd.Detail).Returns("Something went wrong");
		A.CallTo(() => pd.Type).Returns("validation_error");
		A.CallTo(() => pd.ErrorCode).Returns(400);
		A.CallTo(() => failedResult.ProblemDetails).Returns(pd);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(failedResult));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("failed");
		activity.GetTagItem("error.type").ShouldBe("validation_error");
		activity.GetTagItem("error.code").ShouldBe(400);
		activity.Dispose();
	}

	[Fact]
	public async Task SetErrorStatus_OnFailedResult_WithNullProblemDetails()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.IsSuccess).Returns(false);
		A.CallTo(() => failedResult.ProblemDetails).Returns(null);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(failedResult));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("failed");
		activity.Dispose();
	}

	[Fact]
	public async Task SetExceptionStatus_OnException()
	{
		// Arrange
		var captured = new ConcurrentBag<Activity>();
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == DispatchActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
			ActivityStarted = activity => captured.Add(activity),
		};
		ActivitySource.AddActivityListener(listener);

		var middleware = new TracingMiddleware(DefaultOptions, A.Fake<ITelemetrySanitizer>());
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(message, context, (_, _, _) =>
				throw new InvalidOperationException("Test error"), CancellationToken.None));

		// Find activity matching OUR unique message ID to avoid cross-test interference
		var activity = captured.FirstOrDefault(a => a.GetTagItem("message.id")?.ToString() == uniqueId);
		activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("exception");
		activity.Dispose();
	}

	[Fact]
	public async Task SetMessageTypeTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.type").ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("handle");
		activity.Dispose();
	}

	[Fact]
	public async Task SetHandlerTypeTag_WhenAvailable()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["HandlerType"] = typeof(string),
		};
		var context = CreateFakeContext(messageId: uniqueId, items: items);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("handler.type").ShouldBe("String");
		activity.Dispose();
	}

	[Fact]
	public async Task SetMessageKind_ForAction()
	{
		// Arrange
		var message = A.Fake<IDispatchAction>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.kind").ShouldBe("Action");
		activity.Dispose();
	}

	[Fact]
	public async Task SetMessageKind_ForEvent()
	{
		// Arrange
		var message = A.Fake<IDispatchEvent>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.kind").ShouldBe("Event");
		activity.Dispose();
	}

	[Fact]
	public async Task SetMessageKind_ForDocument()
	{
		// Arrange
		var message = A.Fake<IDispatchDocument>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.kind").ShouldBe("Document");
		activity.Dispose();
	}

	[Fact]
	public async Task SetMessageKind_ForGenericMessage()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var uniqueId = Guid.NewGuid().ToString();
		var context = CreateFakeContext(messageId: uniqueId);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);

		// Act
		var activity = await InvokeAndCaptureActivity(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(result));

		// Assert
		activity.ShouldNotBeNull();
		activity.GetTagItem("message.kind").ShouldBe("Message");
		activity.Dispose();
	}
}
