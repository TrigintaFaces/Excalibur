// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Middleware.Inbox;

/// <summary>
/// Author≠impl behavioral regression lock for S859 ADR-336 · <c>t9pnvh</c> (thin-augment) —
/// <see cref="InboxMiddleware"/> must <b>actually reprocess</b> a message whose durable inbox entry is
/// stuck in <see cref="InboxStatus.Processing"/> beyond <see cref="InboxConfigurationOptions.ProcessingTimeout"/>
/// (a crashed/abandoned prior attempt), not merely emit the <c>timeout-reset</c> metric disposition.
/// </summary>
/// <remarks>
/// The pre-existing metric lock (<c>InboxMiddlewareMetricsShould.RecordDeduplicated_OnFullModeProcessingTimeout</c>)
/// asserts only that the <c>dispatch.inbox.deduplicated{timeout-reset,full}</c> counter fires — i.e. the
/// <i>disposition</i>. It does NOT prove the handler is re-invoked or the entry re-finalized. This lock
/// closes that gap: a stuck-Processing entry past the timeout MUST fall through to reprocessing — the
/// handler IS invoked, the success result flows back, and the store is re-marked Processed via
/// <see cref="IInboxStore.MarkProcessedAsync"/>.
/// <para>
/// <b>RED on regression:</b> if the <c>case InboxStatus.Processing</c> timeout fall-through
/// (<c>InboxMiddleware.cs</c> ~L453-460) were removed, the stuck entry would be treated as a duplicate
/// and short-circuited — the handler would NOT run and <c>MarkProcessedAsync</c> would NOT be called,
/// failing every assertion below. Non-vacuous against a dropped reclaim.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxMiddlewareReclaimShould
{
	private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(5);

	[Fact]
	public async Task ReprocessAndRefinalize_WhenEntryIsStuckInProcessingPastTimeout()
	{
		// Arrange — a durable entry abandoned mid-Processing 2× the timeout ago (crashed prior attempt).
		var stuckEntry = new InboxEntry
		{
			Status = InboxStatus.Processing,
			LastAttemptAt = DateTimeOffset.UtcNow - (2 * ProcessingTimeout),
		};

		var store = A.Fake<IInboxStore>();
		_ = A.CallTo(() => store.GetEntryAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry?>(stuckEntry));

		var options = Microsoft.Extensions.Options.Options.Create(
			new InboxConfigurationOptions { Enabled = true, ProcessingTimeout = ProcessingTimeout });
		// 15sf7a injected DispatchJsonSerializer into the InboxMiddleware ctor (required, fail-loud) —
		// pass a default instance; this reclaim lock doesn't exercise serialization (no new-entry path).
		var middleware = new InboxMiddleware(options, store, deduplicator: null, new DispatchJsonSerializer(), NullLogger<InboxMiddleware>.Instance);

		var handlerInvoked = false;
		var successResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => successResult.Succeeded).Returns(true);
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerInvoked = true;
			return new ValueTask<IMessageResult>(successResult);
		};

		// Act
		var result = await middleware.InvokeAsync(
			A.Fake<IDispatchMessage>(), CreateContext(), next, CancellationToken.None);

		// Assert — the stuck entry was RECLAIMED and genuinely reprocessed end-to-end:
		handlerInvoked.ShouldBeTrue(
			"a Processing entry past ProcessingTimeout must fall through to reprocessing — the handler must be re-invoked, not skipped as a duplicate");
		result.Succeeded.ShouldBeTrue("the reprocessed handler's success result must flow back through the middleware");
		A.CallTo(() => store.MarkProcessedAsync(A<string>._, A<string>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private static IMessageContext CreateContext()
	{
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		_ = A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		_ = A.CallTo(() => context.MessageId).Returns("inbox-reclaim-msg");
		return context;
	}
}
