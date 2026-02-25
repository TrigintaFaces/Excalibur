// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Unit tests for MetricsMiddleware.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MetricsMiddlewareShould : UnitTestBase
{
	private readonly IDispatchMetrics _metrics;
	private readonly MetricsMiddleware _middleware;

	public MetricsMiddlewareShould()
	{
		_metrics = A.Fake<IDispatchMetrics>();
		_middleware = new MetricsMiddleware(_metrics);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenMetricsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MetricsMiddleware(null!));
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
	public async Task InvokeAsync_RecordsProcessingDuration_OnSuccess()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordProcessingDuration(A<double>._, "TestMetricMessage", true))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_RecordsMessageProcessed_OnSuccess()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordMessageProcessed("TestMetricMessage", A<string>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_RecordsProcessingDuration_OnFailure()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		var problemDetails = new MessageProblemDetails { Title = "Error", Type = "validation-error" };
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Failed(problemDetails));

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordProcessingDuration(A<double>._, "TestMetricMessage", false))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_RecordsMessageFailed_OnFailure()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		var problemDetails = new MessageProblemDetails { Title = "Error", Type = "validation-error" };
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Failed(problemDetails));

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordMessageFailed("TestMetricMessage", "validation-error", 0))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_RecordsMessageFailed_WithUnknownType_WhenTypeIsNull()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		var problemDetails = new MessageProblemDetails { Title = "Error", Type = null! };
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Failed(problemDetails));

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordMessageFailed("TestMetricMessage", "unknown", 0))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_RecordsMetrics_OnException()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => throw new InvalidOperationException("Test");

		// Act
		try
		{
			_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		A.CallTo(() => _metrics.RecordProcessingDuration(A<double>._, "TestMetricMessage", false))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => _metrics.RecordMessageFailed("TestMetricMessage", "InvalidOperationException", 0))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_RethrowsException()
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
	public async Task InvokeAsync_ReturnsResultFromNext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = MessageResult.Success();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvokeAsync_UsesHandlerTypeFromContext_WhenAvailable()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(typeof(TestHandler));
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordMessageProcessed("TestMetricMessage", "TestHandler"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_UsesUnknown_WhenHandlerTypeNotAvailable()
	{
		// Arrange
		var message = new TestMetricMessage();
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		_ = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => _metrics.RecordMessageProcessed("TestMetricMessage", "Unknown"))
			.MustHaveHappenedOnceExactly();
	}

	private sealed record TestMetricMessage : IDispatchMessage;

	private sealed class TestHandler;
}
