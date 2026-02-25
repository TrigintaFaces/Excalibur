// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Http;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Http;

/// <summary>
/// Unit tests for <see cref="W3CTraceContextMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class W3CTraceContextMiddlewareShould : UnitTestBase
{
	private readonly W3CTraceContextMiddleware _middleware = new();

	[Fact]
	public void Stage_IsPreProcessing()
	{
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void TraceparentKey_IsCorrect()
	{
		W3CTraceContextMiddleware.TraceparentKey.ShouldBe("traceparent");
	}

	[Fact]
	public void TracestateKey_IsCorrect()
	{
		W3CTraceContextMiddleware.TracestateKey.ShouldBe("tracestate");
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenMessageIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(null!, context, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(message, null!, next, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsArgumentNullException_WhenNextDelegateIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(message, context, null!, CancellationToken.None));
	}

	[Fact]
	public async Task InvokeAsync_CallsNext_WhenNoTraceparentHeader()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>("traceparent")).Returns(null);
		A.CallTo(() => context.GetItem<IDictionary<string, string>>("Headers")).Returns(null);

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_CallsNext_WhenTraceparentIsPresent()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var traceparent = $"00-{traceId}-{spanId}-01";

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>("traceparent")).Returns(traceparent);

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_ExtractsTraceparentFromHeaders()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var traceparent = $"00-{traceId}-{spanId}-01";
		var headers = new Dictionary<string, string> { ["traceparent"] = traceparent };

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>("traceparent")).Returns(null);
		A.CallTo(() => context.GetItem<IDictionary<string, string>>("Headers")).Returns(headers);

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeAsync_CallsNext_WhenTraceparentIsInvalid()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>("traceparent")).Returns("invalid-traceparent");

		var nextCalled = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			nextCalled = true;
			return ValueTask.FromResult(MessageResult.Success());
		};

		// Act
		var result = await _middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}
}
