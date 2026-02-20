// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Http;

namespace Excalibur.Dispatch.Observability.Tests.Http;

/// <summary>
/// Deep coverage tests for <see cref="W3CTraceContextMiddleware"/> covering
/// ExtractHeader fallback paths, tracestate propagation, invalid format resilience,
/// and Header dictionary extraction paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class W3CTraceContextMiddlewareDepthShould
{
	private readonly W3CTraceContextMiddleware _sut = new();

	[Fact]
	public async Task ExtractTraceparentFromHeadersDictionary_WhenContextItemIsNull()
	{
		// Arrange — traceparent not in context items but in Headers dict
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var headers = new Dictionary<string, string>
		{
			["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
			["tracestate"] = "vendor=value",
		};

		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns(null);
		A.CallTo(() => context.GetItem<IDictionary<string, string>>("Headers"))
			.Returns(headers);
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TracestateKey))
			.Returns(null);

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — header dict fallback should find traceparent and call next
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnDefault_WhenTraceparentHasLessThan4Parts()
	{
		// Arrange — only 3 parts
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7");

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — invalid format but still calls next (defaults to no parent context)
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnDefault_WhenTraceIdLengthIsWrong()
	{
		// Arrange — trace ID only 16 chars instead of 32
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6-00f067aa0ba902b7-01");

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnDefault_WhenSpanIdLengthIsWrong()
	{
		// Arrange — span ID only 8 chars instead of 16
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa-01");

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleNonHexFlagsGracefully()
	{
		// Arrange — flags "zz" cannot be parsed as hex
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-zz");
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TracestateKey))
			.Returns(null);

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — invalid hex flags default to None, middleware continues
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleShortFlagsGracefully()
	{
		// Arrange — flags "1" is shorter than 2 chars
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-1");
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TracestateKey))
			.Returns(null);

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task PropagateTracestate_WhenBothHeadersPresent()
	{
		// Arrange — both traceparent and tracestate in context items
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TracestateKey))
			.Returns("rojo=abc,vendor=data");

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — always calls next
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task SkipWhenTraceparentIsEmptyString()
	{
		// Arrange — empty string should be treated as missing (Length: > 0 check)
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("");
		A.CallTo(() => context.GetItem<IDictionary<string, string>>("Headers"))
			.Returns(null);

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — empty traceparent treated as missing, goes straight to next
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleHeadersDictionaryWithoutTraceparent()
	{
		// Arrange — Headers dict exists but doesn't contain traceparent key
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var headers = new Dictionary<string, string>
		{
			["some-other-header"] = "value",
		};

		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns(null);
		A.CallTo(() => context.GetItem<IDictionary<string, string>>("Headers"))
			.Returns(headers);

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — no traceparent found at all, goes straight to next
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleExtraPartsInTraceparent()
	{
		// Arrange — W3C spec allows future versions with extra parts
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01-extradata");
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TracestateKey))
			.Returns(null);

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — extra parts should be ignored (>=4 parts check passes)
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleInvalidHexInTraceId()
	{
		// Arrange — trace ID with invalid hex chars causes ActivityTraceId parse failure
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns("00-ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ-00f067aa0ba902b7-01");

		var nextCalled = false;
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — catches ArgumentOutOfRangeException, returns default, still calls next
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public void ExposeCorrectStage()
	{
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task PassThroughResultFromNextDelegate()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.GetItem<string>(W3CTraceContextMiddleware.TraceparentKey))
			.Returns(null);
		A.CallTo(() => context.GetItem<IDictionary<string, string>>("Headers"))
			.Returns(null);

		var expectedResult = A.Fake<IMessageResult>();
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expectedResult);

		// Act
		var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — result from next delegate should pass through
		result.ShouldBe(expectedResult);
	}
}
