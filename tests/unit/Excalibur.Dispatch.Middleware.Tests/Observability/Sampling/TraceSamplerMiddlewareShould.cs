// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sampling;

/// <summary>
/// Unit tests for <see cref="TraceSamplerMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceSamplerMiddlewareShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenSamplerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new TraceSamplerMiddleware(null!));
	}

	[Fact]
	public void Stage_ReturnsStart()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Start);
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenDelegateIsNull()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_CallsNextDelegate()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(true);

		var middleware = new TraceSamplerMiddleware(sampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var nextCalled = false;

		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_SetsContextFlag_WhenNotSampled()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(false);

		var middleware = new TraceSamplerMiddleware(sampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => context.SetItem("dispatch.trace.sampled", false))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_DoesNotSetContextFlag_WhenSampled()
	{
		// Arrange
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(true);

		var middleware = new TraceSamplerMiddleware(sampler);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act
		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		A.CallTo(() => context.SetItem("dispatch.trace.sampled", A<object>._))
			.MustNotHaveHappened();
	}
}
