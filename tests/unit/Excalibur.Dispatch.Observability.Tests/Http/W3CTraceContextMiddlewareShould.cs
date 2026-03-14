// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Http;

namespace Excalibur.Dispatch.Observability.Tests.Http;

/// <summary>
/// Unit tests for <see cref="W3CTraceContextMiddleware"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class W3CTraceContextMiddlewareShould
{
	private readonly W3CTraceContextMiddleware _sut = new();

	/// <summary>
	/// Creates a fake <see cref="IMessageContext"/> backed by a real Items dictionary
	/// so that extension methods (GetItem, SetItem, ContainsItem) work correctly.
	/// </summary>
	private static IMessageContext CreateFakeContext(Dictionary<string, object>? items = null)
	{
		var context = A.Fake<IMessageContext>();
		var itemsDict = items ?? new Dictionary<string, object>(StringComparer.Ordinal);
		A.CallTo(() => context.Items).Returns(itemsDict);
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task InvokeAsync_ShouldCallNext_WhenNoTraceparentHeader()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = CreateFakeContext();
		// No traceparent or headers in Items

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
	public async Task InvokeAsync_ShouldCallNext_WhenValidTraceparentProvided()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[W3CTraceContextMiddleware.TraceparentKey] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
		};
		var context = CreateFakeContext(items);

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
	public async Task InvokeAsync_ShouldCallNext_WhenTraceparentIsInvalid()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[W3CTraceContextMiddleware.TraceparentKey] = "invalid-traceparent",
		};
		var context = CreateFakeContext(items);

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
	public async Task InvokeAsync_ShouldExtractTraceparentFromHeaders()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var headers = new Dictionary<string, string>
		{
			["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
		};
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Headers"] = headers,
		};
		var context = CreateFakeContext(items);

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
	public async Task InvokeAsync_ShouldThrow_WhenMessageIsNull()
	{
		// Arrange
		var context = A.Fake<IMessageContext>();
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ShouldThrow_WhenContextIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task InvokeAsync_ShouldThrow_WhenNextDelegateIsNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
	}

	[Fact]
	public void TraceparentKey_ShouldBeStandardW3CKey()
	{
		W3CTraceContextMiddleware.TraceparentKey.ShouldBe("traceparent");
	}

	[Fact]
	public void TracestateKey_ShouldBeStandardW3CKey()
	{
		W3CTraceContextMiddleware.TracestateKey.ShouldBe("tracestate");
	}
}
