// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable CA2012 // FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Threading;
using Excalibur.Dispatch.Threading;

using Microsoft.Extensions.Logging.Abstractions;

using MicrosoftOptions = Microsoft.Extensions.Options.Options;

using Tests.Shared.TestDoubles;

namespace Excalibur.Dispatch.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="BackgroundExecutionMiddleware"/> verifying immediate return for background
/// dispatch and exception isolation. The failure policy and the caller-token contract are bound in
/// <c>BackgroundExecutionPolicyShould</c>.
/// Sprint 560 (S560.44).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Middleware)]
public sealed class BackgroundExecutionMiddlewareShould : UnitTestBase
{
	private readonly BackgroundExecutionMiddleware _middleware;
	private readonly TestMessageContext _context;

	public BackgroundExecutionMiddlewareShould()
	{
		_middleware = new BackgroundExecutionMiddleware(
			MicrosoftOptions.Create(new BackgroundExecutionOptions()),
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		_context = new TestMessageContext
		{
			MessageId = Guid.NewGuid().ToString(),
			MessageType = "TestMessage",
		};
	}

	[Fact]
	public async Task PassThroughForNonBackgroundMessages()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var nextInvoked = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextInvoked = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		}

		// Act
		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		nextInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnSuccessImmediatelyForBackgroundMessages()
	{
		// Arrange
		var message = new TestBackgroundMessage();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert. Succeeded only -- what the caller can DISTINGUISH is bound by
		// ReportTheWorkAsPending_NotAsHandled, below.
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectBackgroundMessagesExpectingTypedResult()
	{
		// Arrange
		var message = new TestBackgroundActionWithResult();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe(ProblemDetailsTypes.BackgroundExecution);
	}

	/// <summary>
	/// SAFETY. A message handed to a background worker is reported as PENDING, never as handled.
	/// </summary>
	/// <remarks>
	/// The defect this binds: the middleware returned an undifferentiated success for a handler that had
	/// not run and might never run, so a caller recording completion marked work done that nothing had
	/// processed. Succeeded stays true -- the dispatch did succeed -- and the disposition is what
	/// carries the fact that the work is outstanding. RED against restoring MessageResult.Success().
	/// </remarks>
	[Fact]
	public async Task ReportTheWorkAsPending_NotAsHandled()
	{
		var message = new TestBackgroundMessage();
		static ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		result.Succeeded.ShouldBeTrue("the dispatch itself succeeded; only the handler is outstanding");
		result.Disposition.ShouldBe(
			MessageDisposition.AcceptedForBackgroundExecution,
			"a caller recording completion cannot tell deferred work from handled work, so it marks as "
				+ "done a message no handler has processed");
	}

	/// <summary>
	/// LIVENESS. A message that is NOT deferred still reports Handled.
	/// </summary>
	/// <remarks>
	/// Without this arm a middleware that reported AcceptedForBackgroundExecution for every message would
	/// satisfy the safety arm above, while telling every caller in the system that nothing was ever
	/// handled.
	/// </remarks>
	[Fact]
	public async Task StillReportHandled_WhenTheMessageIsNotDeferred()
	{
		var message = A.Fake<IDispatchMessage>();
		static ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		result.Disposition.ShouldBe(MessageDisposition.Handled);
	}

	/// <summary>
	/// SAFETY. The discarded 202 problem details do not come back as an error on a successful result.
	/// </summary>
	/// <remarks>
	/// IMessageResult.ProblemDetails is documented as non-null only when the operation FAILS, and
	/// IMessageProblemDetails is RFC 7807 -- details of ERRORS, with Title defaulting to "Error". A 202
	/// carried there would make a caller testing ProblemDetails for failure read accepted work as failed.
	/// </remarks>
	[Fact]
	public async Task NotCarryProblemDetailsOnAnAcceptedResult()
	{
		var message = new TestBackgroundMessage();
		static ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		var result = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		result.ProblemDetails.ShouldBeNull(
			"problem details on a succeeded result contradict the documented contract, so a caller that "
				+ "branches on them would treat accepted work as failed");
	}

	[Fact]
	public void SetStageNearEnd()
	{
		// Stage is End - 1 to execute near the end of the pipeline
		_middleware.Stage.ShouldBe(DispatchMiddlewareStage.End - 1);
	}

	[Fact]
	public async Task NotCallNextDelegateSynchronouslyForBackgroundMessages()
	{
		// Arrange
		var message = new TestBackgroundMessage();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act
		_ = await _middleware.InvokeAsync(message, _context, Next, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — The fire-and-forget dispatch may or may not have started yet,
		// but we can verify we got a result back immediately.
		// Give it a brief moment to process in background
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(100).ConfigureAwait(false);

		// The next delegate should eventually be called in background
		// This is a fire-and-forget test - the key behavior is immediate return
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionForNullMessage()
	{
		// Arrange
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
			=> new(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _middleware.InvokeAsync(null!, _context, Next, CancellationToken.None)
				.ConfigureAwait(false))
			.ConfigureAwait(false);
	}

	// Test message types
	private sealed class TestBackgroundMessage : IDispatchMessage, IExecuteInBackground;

	private sealed class TestBackgroundActionWithResult : IDispatchAction<string>, IExecuteInBackground;
}

