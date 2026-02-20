// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Unit tests for TracingMiddleware.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TracingMiddlewareShould : UnitTestBase
{
	private readonly ActivityListener _activityListener;
	private readonly List<Activity> _capturedActivities = [];
	private readonly TracingMiddleware _middleware;

	public TracingMiddlewareShould()
	{
		_middleware = new TracingMiddleware(NullTelemetrySanitizer.Instance);

		// Set up activity listener to capture activities
		_activityListener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == DispatchActivitySource.Name,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
			ActivityStarted = activity => _capturedActivities.Add(activity)
		};
		ActivitySource.AddActivityListener(_activityListener);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_activityListener.Dispose();
			foreach (var activity in _capturedActivities)
			{
				activity.Dispose();
			}
		}

		base.Dispose(disposing);
	}

	[Fact]
	public void Stage_ReturnsPreProcessing()
	{
		// Assert
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_CallsNextDelegate()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsSuccessResult_WhenNextReturnsSuccess()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ReturnsFailedResult_WhenNextReturnsFailed()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var problemDetails = new MessageProblemDetails { Title = "Test Error", Status = 400 };
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Failed(problemDetails));

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task InvokeAsync_RethrowsException_WhenNextThrows()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedException = new InvalidOperationException("Test exception");

		DispatchRequestDelegate next = (_, _, _) => throw expectedException;

		// Act & Assert
		var thrownException = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _middleware.InvokeAsync(message, context, next, CancellationToken.None));

		thrownException.Message.ShouldBe("Test exception");
	}

	[Fact]
	public async Task InvokeAsync_CreatesActivity_WhenListenerIsRegistered()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("test-message-id");
		_ = A.CallTo(() => context.CorrelationId).Returns("test-correlation-id");

		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());
		var countBefore = _capturedActivities.Count;

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		_capturedActivities.Count.ShouldBeGreaterThan(countBefore);
	}

	[Fact]
	public async Task InvokeAsync_SetsMessageTypeTag()
	{
		// Arrange
		var message = new TestMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());
		var countBefore = _capturedActivities.Count;

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — use activity created by this test, not stray activities from parallel tests
		var activity = _capturedActivities.Skip(countBefore).FirstOrDefault();
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem("message.type").ShouldBe("TestMessage");
	}

	[Fact]
	public async Task InvokeAsync_SetsMessageIdTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns("my-message-id");
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());
		var countBefore = _capturedActivities.Count;

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — use activity created by this test, not stray activities from parallel tests
		var activity = _capturedActivities.Skip(countBefore).FirstOrDefault();
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem("message.id").ShouldBe("my-message-id");
	}

	[Fact]
	public async Task InvokeAsync_SetsDispatchOperationTag()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());
		var countBefore = _capturedActivities.Count;

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — use activity created by this test, not stray activities from parallel tests
		var activity = _capturedActivities.Skip(countBefore).FirstOrDefault();
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.operation").ShouldBe("handle");
	}

	[Fact]
	public async Task InvokeAsync_SetsSuccessStatus_OnSuccess()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());
		var countBefore = _capturedActivities.Count;

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — use activity created by this test, not stray activities from parallel tests
		var activity = _capturedActivities.Skip(countBefore).FirstOrDefault();
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("success");
		activity.Status.ShouldBe(ActivityStatusCode.Ok);
	}

	[Fact]
	public async Task InvokeAsync_SetsErrorStatus_OnFailure()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var problemDetails = new MessageProblemDetails { Title = "Error", Status = 500, Detail = "Test error" };
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Failed(problemDetails));
		var countBefore = _capturedActivities.Count;

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — use activity created by this test, not stray activities from parallel tests
		var activity = _capturedActivities.Skip(countBefore).FirstOrDefault();
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("failed");
		activity.Status.ShouldBe(ActivityStatusCode.Error);
	}

	[Fact]
	public async Task InvokeAsync_SetsExceptionStatus_OnException()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Test");
		var countBefore = _capturedActivities.Count;

		// Act
		try
		{
			_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert — use activity created by this test, not stray activities from parallel tests
		var activity = _capturedActivities.Skip(countBefore).FirstOrDefault();
		_ = activity.ShouldNotBeNull();
		activity.GetTagItem("dispatch.status").ShouldBe("exception");
		activity.Status.ShouldBe(ActivityStatusCode.Error);
	}

	private sealed record TestMessage : IDispatchMessage;
}
