// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class MetricsMiddlewareShould
{
	private readonly IDispatchMetrics _fakeMetrics = A.Fake<IDispatchMetrics>();

	[Fact]
	public void ThrowOnNullMetrics()
	{
		Should.Throw<ArgumentNullException>(() => new MetricsMiddleware(null!));
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		var middleware = new MetricsMiddleware(_fakeMetrics);
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task InvokeNextDelegate_AndRecordMetrics()
	{
		// Arrange
		var middleware = new MetricsMiddleware(_fakeMetrics);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.IsSuccess).Returns(true);

		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeMetrics.RecordProcessingDuration(
			A<double>._, A<string>._, true)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeMetrics.RecordMessageProcessed(
			A<string>._, A<string>._, A<(string Key, object? Value)[]>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailure_WhenResultIsNotSuccess()
	{
		// Arrange
		var middleware = new MetricsMiddleware(_fakeMetrics);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.IsSuccess).Returns(false);
		A.CallTo(() => failedResult.ProblemDetails).Returns(A.Fake<IMessageProblemDetails>());

		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(failedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(failedResult);
		A.CallTo(() => _fakeMetrics.RecordMessageFailed(
			A<string>._, A<string>._, 0)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RecordFailure_WhenExceptionIsThrown()
	{
		// Arrange
		var middleware = new MetricsMiddleware(_fakeMetrics);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		DispatchRequestDelegate next = (msg, ctx, ct) => throw new InvalidOperationException("Test");

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			async () => await middleware.InvokeAsync(message, context, next, CancellationToken.None));

		A.CallTo(() => _fakeMetrics.RecordProcessingDuration(
			A<double>._, A<string>._, false)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeMetrics.RecordMessageFailed(
			A<string>._, "InvalidOperationException", 0)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var middleware = new MetricsMiddleware(_fakeMetrics);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, A.Fake<IMessageContext>(), next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var middleware = new MetricsMiddleware(_fakeMetrics);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var middleware = new MetricsMiddleware(_fakeMetrics);

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}
}
