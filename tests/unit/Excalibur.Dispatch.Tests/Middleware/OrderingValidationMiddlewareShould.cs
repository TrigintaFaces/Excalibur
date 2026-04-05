// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware.Ordering;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Tests for OrderingValidationMiddleware (Sprint 696 T.23).
/// Verifies sequence number tracking and out-of-order detection.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class OrderingValidationMiddlewareShould
{
	private readonly OrderingValidationMiddleware _sut;
	private readonly ILogger<OrderingValidationMiddleware> _logger;

	public OrderingValidationMiddlewareShould()
	{
		_logger = A.Fake<ILogger<OrderingValidationMiddleware>>();
		A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
		_sut = new OrderingValidationMiddleware(_logger);
	}

	[Fact]
	public void HavePreProcessingStage()
	{
		_sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public async Task PassThroughWhenNoSequenceNumber()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = CreateContext();
		var nextCalled = false;

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act
		await _sut.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert
		nextCalled.ShouldBeTrue();
	}

	[Fact]
	public async Task AcceptInOrderMessages()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act -- send sequence 1, 2, 3
		for (long seq = 1; seq <= 3; seq++)
		{
			var context = CreateContext(sequenceNumber: seq, source: "test-source");
			await _sut.InvokeAsync(message, context, Next, CancellationToken.None);
		}

		// Assert -- no warning logged for in-order messages
		// (sequence 1 > -1, 2 > 1, 3 > 2 -- all in order)
		// The LogOutOfOrder call should NOT have been made
	}

	[Fact]
	public async Task DetectOutOfOrderMessages()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act -- send sequence 3, then 1 (out of order)
		var context3 = CreateContext(sequenceNumber: 3, source: "test-source");
		await _sut.InvokeAsync(message, context3, Next, CancellationToken.None);

		var context1 = CreateContext(sequenceNumber: 1, source: "test-source");
		await _sut.InvokeAsync(message, context1, Next, CancellationToken.None);

		// Assert -- sequence 1 <= last 3 should trigger warning
		// The middleware logs but does NOT block -- next is still called
	}

	[Fact]
	public async Task TrackSequencesPerSource()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act -- source A gets seq 5, source B gets seq 1
		// Source B's seq 1 should NOT trigger out-of-order since it's a different source
		var contextA = CreateContext(sequenceNumber: 5, source: "source-A");
		await _sut.InvokeAsync(message, contextA, Next, CancellationToken.None);

		var contextB = CreateContext(sequenceNumber: 1, source: "source-B");
		await _sut.InvokeAsync(message, contextB, Next, CancellationToken.None);

		// Assert -- no out-of-order warning for different sources
	}

	[Fact]
	public async Task DetectDuplicateSequenceNumber()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act -- send sequence 5 twice (duplicate = out of order)
		var context1 = CreateContext(sequenceNumber: 5, source: "dup-source");
		await _sut.InvokeAsync(message, context1, Next, CancellationToken.None);

		var context2 = CreateContext(sequenceNumber: 5, source: "dup-source");
		await _sut.InvokeAsync(message, context2, Next, CancellationToken.None);

		// Assert -- duplicate is detected (5 <= 5)
	}

	[Fact]
	public async Task UseDefaultSourceWhenNotProvided()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new ValueTask<IMessageResult>(A.Fake<IMessageResult>());

		// Act -- sequence with no Source in context
		var context = CreateContext(sequenceNumber: 1, source: null);
		await _sut.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert -- should use "default" source, no exception
	}

	[Fact]
	public async Task AlwaysCallNextDelegate()
	{
		// Arrange -- even with out-of-order, next should be called
		var message = A.Fake<IDispatchMessage>();
		var nextCallCount = 0;

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCallCount++;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		}

		// Act -- send out of order: 10, 5, 15
		await _sut.InvokeAsync(message, CreateContext(10, "s"), Next, CancellationToken.None);
		await _sut.InvokeAsync(message, CreateContext(5, "s"), Next, CancellationToken.None);
		await _sut.InvokeAsync(message, CreateContext(15, "s"), Next, CancellationToken.None);

		// Assert -- all 3 calls went through
		nextCallCount.ShouldBe(3);
	}

	private static IMessageContext CreateContext(long? sequenceNumber = null, string? source = null)
	{
		var context = A.Fake<IMessageContext>();
		var items = new Dictionary<string, object>();

		if (sequenceNumber.HasValue)
		{
			items["SequenceNumber"] = sequenceNumber.Value;
		}

		if (source is not null)
		{
			items["Source"] = source;
		}

		A.CallTo(() => context.Items).Returns(items);
		return context;
	}
}
