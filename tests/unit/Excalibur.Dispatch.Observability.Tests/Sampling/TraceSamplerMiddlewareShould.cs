// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

/// <summary>
/// Unit tests for <see cref="TraceSamplerMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sampling")]
public sealed class TraceSamplerMiddlewareShould
{
	private readonly ITraceSampler _fakeSampler = A.Fake<ITraceSampler>();

	[Fact]
	public void ThrowOnNullSampler()
	{
		Should.Throw<ArgumentNullException>(() => new TraceSamplerMiddleware(null!));
	}

	[Fact]
	public void HaveStartStage()
	{
		var middleware = new TraceSamplerMiddleware(_fakeSampler);
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Start);
	}

	[Fact]
	public async Task SetSampledFlag_WhenSamplerReturnsFalse()
	{
		// Arrange
		A.CallTo(() => _fakeSampler.ShouldSample(A<System.Diagnostics.ActivityContext>._, A<string>._))
			.Returns(false);

		var middleware = new TraceSamplerMiddleware(_fakeSampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => context.SetItem("dispatch.trace.sampled", false)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotSetSampledFlag_WhenSamplerReturnsTrue()
	{
		// Arrange
		A.CallTo(() => _fakeSampler.ShouldSample(A<System.Diagnostics.ActivityContext>._, A<string>._))
			.Returns(true);

		var middleware = new TraceSamplerMiddleware(_fakeSampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(expectedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => context.SetItem("dispatch.trace.sampled", A<object>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeNextDelegate()
	{
		// Arrange
		A.CallTo(() => _fakeSampler.ShouldSample(A<System.Diagnostics.ActivityContext>._, A<string>._))
			.Returns(true);

		var middleware = new TraceSamplerMiddleware(_fakeSampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();
		var nextCalled = false;

		DispatchRequestDelegate next = (msg, ctx, ct) =>
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Act
		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var middleware = new TraceSamplerMiddleware(_fakeSampler);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, A.Fake<IMessageContext>(), next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var middleware = new TraceSamplerMiddleware(_fakeSampler);
		DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var middleware = new TraceSamplerMiddleware(_fakeSampler);

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}
}
