// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing.Tracking;

namespace Excalibur.Dispatch.Testing.Tests.Tracking;

[Trait("Category", "Unit")]
[Trait("Component", "Excalibur.Dispatch.Testing")]
public sealed class TestTrackingMiddlewareShould
{
	[Fact]
	public void HaveStartStage()
	{
		var log = new DispatchedMessageLog();
		var middleware = new TestTrackingMiddleware(log);

		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Start);
	}

	[Fact]
	public async Task RecordDispatchedMessage()
	{
		var log = new DispatchedMessageLog();
		var middleware = new TestTrackingMiddleware(log);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

#pragma warning disable CA2012
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expectedResult);
#pragma warning restore CA2012

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		log.Count.ShouldBe(1);
		var recorded = log.All[0];
		recorded.Message.ShouldBeSameAs(message);
		recorded.Context.ShouldBeSameAs(context);
		recorded.Result.ShouldBeSameAs(expectedResult);
	}

	[Fact]
	public async Task RecordTimestampAtStartOfInvocation()
	{
		var log = new DispatchedMessageLog();
		var middleware = new TestTrackingMiddleware(log);
		var before = DateTimeOffset.UtcNow;

#pragma warning disable CA2012
		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
#pragma warning restore CA2012

		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), next, CancellationToken.None);

		var after = DateTimeOffset.UtcNow;
		var recorded = log.All[0];
		recorded.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		recorded.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public async Task CallNextDelegateAndReturnResult()
	{
		var log = new DispatchedMessageLog();
		var middleware = new TestTrackingMiddleware(log);
		var expectedResult = A.Fake<IMessageResult>();
		var delegateCalled = false;

#pragma warning disable CA2012
		DispatchRequestDelegate next = (_, _, _) =>
		{
			delegateCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};
#pragma warning restore CA2012

		var result = await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), next, CancellationToken.None);

		delegateCalled.ShouldBeTrue();
		result.ShouldBeSameAs(expectedResult);
	}

	[Fact]
	public async Task PassCancellationTokenToNextDelegate()
	{
		var log = new DispatchedMessageLog();
		var middleware = new TestTrackingMiddleware(log);
		using var cts = new CancellationTokenSource();
		CancellationToken receivedToken = default;

#pragma warning disable CA2012
		DispatchRequestDelegate next = (_, _, ct) =>
		{
			receivedToken = ct;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};
#pragma warning restore CA2012

		await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(), next, cts.Token);

		receivedToken.ShouldBe(cts.Token);
	}
}
