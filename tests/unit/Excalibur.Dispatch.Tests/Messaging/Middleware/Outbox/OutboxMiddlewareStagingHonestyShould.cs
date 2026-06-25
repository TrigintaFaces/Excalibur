// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .ReturnsLazily() stores a ValueTask

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Outbox;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware.Outbox;

/// <summary>
/// Sprint 847 / Lane H (bead 0yxdja) — author≠impl regression lock for the outbox staging-honesty defect
/// (MS-H) — KEYSTONE (committed floor).
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (true pre-fix HEAD <c>301b4aa62</c>):</b> <c>OutboxMiddleware.StageOutboundMessagesAsync</c>
/// catches a per-message staging failure and, when <c>ContinueOnStagingError == true</c>, swallows it —
/// then unconditionally logs <c>LogStagingSuccess(outboundMessages.Count)</c> (<c>:268</c>), reporting the
/// <em>full attempted</em> count even when one or more messages were NOT staged. That is a false-success /
/// silent outbound-message-loss: "Successfully staged 3 outbound messages" is logged while only 2 staged.
/// </para>
/// <para>
/// <b>Fix (FR-H3/FR-H4):</b> the completion log must report ONLY the count actually staged, and a partial
/// failure must surface a distinct warning/error — "fail open" (don't crash the pipeline) must never mean
/// "report phantom success".
/// </para>
/// <para>
/// <b>Non-vacuity:</b> this lock drives the real middleware through a faked <see cref="IOutboxStore"/> whose
/// 2nd-of-3 <c>StageMessageAsync</c> throws, with <c>ContinueOnStagingError=true</c>, and asserts the
/// captured logs contain NO success-completion record claiming all 3 staged. On the pre-fix HEAD that exact
/// record ("Successfully staged 3 outbound messages") is emitted ⇒ RED. Post-fix it reports 2 (or a
/// partial-failure record) ⇒ GREEN. The assertion binds the STABLE existing <c>LogStagingSuccess</c>
/// contract (it does not depend on the not-yet-named partial-failure log method). The happy-path guard
/// (AC-H1) confirms the full-count success log is still emitted when every message stages.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Feature", "Outbox")]
public sealed class OutboxMiddlewareStagingHonestyShould
{
	private const string OutboundMessagesKey = "OutboundMessages";

	[Fact]
	public async Task NotLogFullSuccessCount_WhenAMessageFailsToStage_ContinueOnError()
	{
		// Arrange — 3 outbound messages; store throws on the 2nd; continue-on-error swallows it.
		var logger = new ListLogger<OutboxMiddleware>();
		var store = A.Fake<IOutboxStore>();
		var stageCalls = 0;
		_ = A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.ReturnsLazily(() =>
			{
				stageCalls++;
				return stageCalls == 2
					? throw new InvalidOperationException("staging store failure on message #2")
					: ValueTask.CompletedTask;
			});

		var middleware = CreateMiddleware(store, logger, continueOnStagingError: true);
		var context = CreateContextWithOutbound(messageCount: 3);

		// Act
		_ = await middleware.InvokeAsync(
			A.Fake<IDispatchEvent>(),
			context,
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None);

		// Assert — the false-success record forbidden by FR-H3 must NOT appear.
		logger.Messages.ShouldNotContain(
			"Successfully staged 3 outbound messages",
			"a partial staging failure (1 of 3 failed) must never be logged as full success — that is " +
			"silent outbound-message-loss (MS-H keystone).");
	}

	[Fact]
	public async Task LogHonestFullSuccessCount_WhenAllMessagesStage()
	{
		// AC-H1 regression guard — all-success path must still report the full staged count.
		var logger = new ListLogger<OutboxMiddleware>();
		var store = A.Fake<IOutboxStore>();
		_ = A.CallTo(() => store.StageMessageAsync(A<OutboundMessage>._, A<CancellationToken>._))
			.Returns(ValueTask.CompletedTask);

		var middleware = CreateMiddleware(store, logger, continueOnStagingError: true);
		var context = CreateContextWithOutbound(messageCount: 3);

		_ = await middleware.InvokeAsync(
			A.Fake<IDispatchEvent>(),
			context,
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None);

		logger.Messages.ShouldContain(
			"Successfully staged 3 outbound messages",
			"when every message stages, the honest full-count success log must be emitted (FR-H1).");
	}

	private static OutboxMiddleware CreateMiddleware(
		IOutboxStore store, ILogger<OutboxMiddleware> logger, bool continueOnStagingError)
	{
		var options = Microsoft.Extensions.Options.Options.Create(new OutboxMiddlewareOptions
		{
			Enabled = true,
			ContinueOnStagingError = continueOnStagingError,
		});
		return new OutboxMiddleware(options, store, logger);
	}

	private static MessageContext CreateContextWithOutbound(int messageCount)
	{
		var context = new MessageContext();
		var outbound = new List<OutboundMessage>();
		for (var i = 0; i < messageCount; i++)
		{
			outbound.Add(new OutboundMessage($"Evt{i}", [(byte)i], $"destination-{i}"));
		}

		context.SetItem(OutboundMessagesKey, outbound);
		return context;
	}

	/// <summary>
	/// Minimal capturing logger. <see cref="IsEnabled"/> returns <see langword="true"/> so source-generated
	/// <c>[LoggerMessage]</c> methods actually emit (a FakeItEasy logger returns false and the source-gen skips Log).
	/// </summary>
	private sealed class ListLogger<T> : ILogger<T>
	{
		private readonly List<string> _messages = [];

		public IReadOnlyList<string> Messages => _messages;

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel, EventId eventId, TState state, Exception? exception,
			Func<TState, Exception?, string> formatter)
			=> _messages.Add(formatter(state, exception));

		private sealed class NoopScope : IDisposable
		{
			public static readonly NoopScope Instance = new();
			public void Dispose() { }
		}
	}
}
