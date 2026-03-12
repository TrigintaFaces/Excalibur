// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Sampling;

namespace Excalibur.Dispatch.Observability.Tests.Sampling;

/// <summary>
/// Functional tests for <see cref="TraceSamplerMiddleware"/> verifying sampling integration with pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sampling")]
public sealed class TraceSamplerMiddlewareFunctionalShould
{
	/// <summary>
	/// Creates a fake <see cref="IMessageContext"/> backed by a real Items dictionary
	/// so that extension methods (GetItem, SetItem, ContainsItem) work correctly.
	/// </summary>
	private static (IMessageContext context, Dictionary<string, object> items) CreateFakeContext()
	{
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(items);
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return (context, items);
	}

	[Fact]
	public void HaveStartStage()
	{
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Start);
	}

	[Fact]
	public void ThrowOnNullSampler()
	{
		Should.Throw<ArgumentNullException>(() => new TraceSamplerMiddleware(null!));
	}

	[Fact]
	public async Task SetSampledFalse_WhenSamplerRejects()
	{
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(false);
		var middleware = new TraceSamplerMiddleware(sampler);

		var message = A.Fake<IDispatchMessage>();
		var (context, items) = CreateFakeContext();
		var result = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		items.ShouldContainKeyAndValue("dispatch.trace.sampled", (object)false);
	}

	[Fact]
	public async Task NotSetSampledFalse_WhenSamplerAccepts()
	{
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(true);
		var middleware = new TraceSamplerMiddleware(sampler);

		var message = A.Fake<IDispatchMessage>();
		var (context, items) = CreateFakeContext();
		var result = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(result);

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		items.ShouldNotContainKey("dispatch.trace.sampled");
	}

	[Fact]
	public async Task InvokeNextDelegate_Regardless()
	{
		var sampler = A.Fake<ITraceSampler>();
		A.CallTo(() => sampler.ShouldSample(A<ActivityContext>._, A<string>._)).Returns(false);
		var middleware = new TraceSamplerMiddleware(sampler);

		var message = A.Fake<IDispatchMessage>();
		var (context, _) = CreateFakeContext();
		var expectedResult = A.Fake<IMessageResult>();
		var nextInvoked = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		nextInvoked.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(null!, A.Fake<IMessageContext>(), next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var sampler = A.Fake<ITraceSampler>();
		var middleware = new TraceSamplerMiddleware(sampler);

		await Should.ThrowAsync<ArgumentNullException>(
			async () => await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), null!, CancellationToken.None));
	}
}
