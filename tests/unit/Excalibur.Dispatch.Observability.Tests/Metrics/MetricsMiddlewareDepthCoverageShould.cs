// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Deep coverage tests for <see cref="MetricsMiddleware"/> covering handler type extraction
/// from context, failed result paths with and without ProblemDetails, and exception-path metrics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class MetricsMiddlewareDepthCoverageShould
{
	private readonly IDispatchMetrics _metrics;
	private readonly MetricsMiddleware _sut;

	public MetricsMiddlewareDepthCoverageShould()
	{
		_metrics = A.Fake<IDispatchMetrics>();
		_sut = new MetricsMiddleware(_metrics);
	}

	[Fact]
	public async Task ExtractHandlerType_WhenAvailableInContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(typeof(string));

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — handler type "String" recorded
		A.CallTo(() => _metrics.RecordMessageProcessed(
			A<string>._,
			"String",
			A<(string Key, object? Value)[]>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task UseUnknownHandlerType_WhenNotInContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<Type>("HandlerType")).Returns(null);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — handler type falls back to "Unknown"
		A.CallTo(() => _metrics.RecordMessageProcessed(
			A<string>._,
			"Unknown",
			A<(string Key, object? Value)[]>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailure_WithProblemDetails_ErrorType()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.IsSuccess).Returns(false);
		var pd = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => pd.Type).Returns("timeout_error");
		A.CallTo(() => failedResult.ProblemDetails).Returns(pd);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(failedResult);

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — error type from ProblemDetails
		A.CallTo(() => _metrics.RecordMessageFailed(
			A<string>._, "timeout_error", 0)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailure_WithNullProblemDetailsType()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.IsSuccess).Returns(false);
		var pd = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => pd.Type).ReturnsLazily(() => (string)null!);
		A.CallTo(() => failedResult.ProblemDetails).Returns(pd);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(failedResult);

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — null Type falls back to "unknown"
		A.CallTo(() => _metrics.RecordMessageFailed(
			A<string>._, "unknown", 0)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotRecordFailure_WhenNullProblemDetails()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.IsSuccess).Returns(false);
		A.CallTo(() => failedResult.ProblemDetails).Returns(null);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(failedResult);

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — no RecordMessageFailed when ProblemDetails is null
		A.CallTo(() => _metrics.RecordMessageFailed(
			A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task RecordDuration_OnException()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => throw new TimeoutException("timed out");

		// Act & Assert
		await Should.ThrowAsync<TimeoutException>(
			async () => await _sut.InvokeAsync(message, context, next, CancellationToken.None));

		// Assert — duration recorded with success=false and exception type as error
		A.CallTo(() => _metrics.RecordProcessingDuration(
			A<double>._, A<string>._, false)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _metrics.RecordMessageFailed(
			A<string>._, "TimeoutException", 0)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordDuration_OnSuccess()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.IsSuccess).Returns(true);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — duration recorded with success=true
		A.CallTo(() => _metrics.RecordProcessingDuration(
			A<double>._, A<string>._, true)).MustHaveHappenedOnceExactly();
	}
}
