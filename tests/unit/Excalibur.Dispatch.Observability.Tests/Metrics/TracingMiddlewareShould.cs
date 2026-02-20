// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="TracingMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class TracingMiddlewareShould : IDisposable
{
	private readonly ITelemetrySanitizer _fakeSanitizer = A.Fake<ITelemetrySanitizer>();
	private readonly ActivityListener _listener;
	private readonly List<Activity> _capturedActivities = [];

	public TracingMiddlewareShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name.Contains("Dispatch", StringComparison.OrdinalIgnoreCase),
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

	[Fact]
	public void ThrowOnNullSanitizer()
	{
		Should.Throw<ArgumentNullException>(() => new TracingMiddleware(null!));
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task InvokeNextDelegate_AndReturnResult()
	{
		// Arrange
		var middleware = new TracingMiddleware(_fakeSanitizer);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		A.CallTo(() => expectedResult.IsSuccess).Returns(true);

		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, A.Fake<IMessageContext>(), next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var middleware = new TracingMiddleware(_fakeSanitizer);

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}
}
