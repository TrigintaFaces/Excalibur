// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="PoisonMessageMiddleware"/> verifying dead letter routing,
/// retry exhaustion, threshold tracking, and cancellation behavior.
/// Sprint 560 (S560.41).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
public sealed class PoisonMessageMiddlewareShould : UnitTestBase
{
	private readonly IPoisonMessageDetector _detector;
	private readonly IPoisonMessageHandler _handler;
	private readonly PoisonMessageOptions _options;
	private readonly PoisonMessageMiddleware _middleware;
	private readonly IDispatchMessage _message;
	private readonly TestMessageContext _context;

	public PoisonMessageMiddlewareShould()
	{
		_detector = A.Fake<IPoisonMessageDetector>();
		_handler = A.Fake<IPoisonMessageHandler>();
		_options = new PoisonMessageOptions { Enabled = true };
		_middleware = new PoisonMessageMiddleware(
			_detector,
			_handler,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<PoisonMessageMiddleware>.Instance);
		_message = A.Fake<IDispatchMessage>();
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
	}

	[Fact]
	public async Task PassThroughWhenDisabled()
	{
		// Arrange
		_options.Enabled = false;
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task PassThroughOnSuccessfulProcessing()
	{
		// Arrange
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		A.CallTo(() => _detector.IsPoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task DetectPoisonMessageAndRouteToDeadLetter()
	{
		// Arrange
		var processingException = new InvalidOperationException("Deserialization failed");
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> throw processingException;

		A.CallTo(() => _detector.IsPoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
			.Returns(PoisonDetectionResult.Poison("Max retries exceeded", "RetryCountDetector"));

		A.CallTo(() => _handler.HandlePoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("PoisonMessage");
		result.ProblemDetails.ErrorCode.ShouldBe(503);

		A.CallTo(() => _handler.HandlePoisonMessageAsync(
			_message, _context, "Max retries exceeded", A<CancellationToken>._, processingException))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RethrowWhenNotPoisonMessage()
	{
		// Arrange
		var processingException = new TimeoutException("Transient failure");
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> throw processingException;

		A.CallTo(() => _detector.IsPoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
			.Returns(PoisonDetectionResult.NotPoison());

		// Act & Assert
		var ex = await Should.ThrowAsync<TimeoutException>(async () =>
			await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
				.ConfigureAwait(false))
			.ConfigureAwait(false);

		ex.Message.ShouldBe("Transient failure");
	}

	[Fact]
	public async Task TrackProcessingAttempts()
	{
		// Arrange
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_context.Items["ProcessingAttempts"].ShouldBe(1);
		_context.Items.ShouldContainKey("FirstAttemptTime");
		_context.Items.ShouldContainKey("CurrentAttemptTime");
		_context.Items.ShouldContainKey("ProcessingHistory");
	}

	[Fact]
	public async Task IncrementAttemptCountAcrossCalls()
	{
		// Arrange
		_context.Items["ProcessingAttempts"] = 2;
		_context.Items["FirstAttemptTime"] = DateTimeOffset.UtcNow.AddSeconds(-10);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_context.Items["ProcessingAttempts"].ShouldBe(3);
	}

	[Fact]
	public async Task RethrowWhenPoisonHandlerFails()
	{
		// Arrange
		var originalException = new InvalidOperationException("Original");
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> throw originalException;

		A.CallTo(() => _detector.IsPoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
			.Returns(PoisonDetectionResult.Poison("Poison", "TestDetector"));

		A.CallTo(() => _handler.HandlePoisonMessageAsync(
			A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
			.ThrowsAsync(new InvalidOperationException("Handler failed"));

		// Act & Assert — the catch block re-throws via 'throw;' which preserves the original
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _middleware.InvokeAsync(_message, _context, Next, CancellationToken.None)
				.ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	[Fact]
	public void SetStageToPreProcessing()
	{
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void DisposeReleasesActivitySourceSafely()
	{
		// Arrange
		var middleware = new PoisonMessageMiddleware(
			_detector,
			_handler,
			Microsoft.Extensions.Options.Options.Create(_options),
			NullLogger<PoisonMessageMiddleware>.Instance);

		// Act & Assert — no exception
		middleware.Dispose();
		middleware.Dispose(); // Double-dispose should be safe
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_middleware.Dispose();
		}

		base.Dispose(disposing);
	}
}
